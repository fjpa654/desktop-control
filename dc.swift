// dc — minimal desktop control for macOS via CGEvent.
// Coordinates are in screen points (e.g. 1440x900 on a 2880x1800 Retina display).
//
// Usage:
//   dc move X Y
//   dc click X Y            (left click)
//   dc dclick X Y           (double click)
//   dc rclick X Y           (right click)
//   dc drag X1 Y1 X2 Y2
//   dc scroll DY [DX]       (lines; negative DY scrolls down)
//   dc type "text"
//   dc key KEY [mods...]    (e.g. dc key s cmd; dc key return; dc key tab shift)
//   dc pos                  (print current mouse position)

import Foundation
import CoreGraphics

let src = CGEventSource(stateID: .hidSystemState)

func die(_ msg: String) -> Never {
    FileHandle.standardError.write((msg + "\n").data(using: .utf8)!)
    exit(1)
}

func pt(_ a: [String], _ i: Int) -> CGPoint {
    guard a.count > i + 1, let x = Double(a[i]), let y = Double(a[i + 1]) else { die("expected X Y") }
    return CGPoint(x: x, y: y)
}

func post(_ e: CGEvent?) {
    e?.post(tap: .cghidEventTap)
    usleep(15_000)
}

func mouse(_ type: CGEventType, _ p: CGPoint, _ btn: CGMouseButton = .left, clicks: Int64 = 1) {
    let e = CGEvent(mouseEventSource: src, mouseType: type, mouseCursorPosition: p, mouseButton: btn)
    e?.setIntegerValueField(.mouseEventClickState, value: clicks)
    post(e)
}

func click(_ p: CGPoint, _ btn: CGMouseButton = .left, count: Int = 1) {
    let (down, up): (CGEventType, CGEventType) = btn == .right ? (.rightMouseDown, .rightMouseUp) : (.leftMouseDown, .leftMouseUp)
    mouse(.mouseMoved, p)
    for i in 1...count {
        mouse(down, p, btn, clicks: Int64(i))
        mouse(up, p, btn, clicks: Int64(i))
    }
}

let keyCodes: [String: CGKeyCode] = [
    "a": 0, "s": 1, "d": 2, "f": 3, "h": 4, "g": 5, "z": 6, "x": 7, "c": 8, "v": 9, "b": 11,
    "q": 12, "w": 13, "e": 14, "r": 15, "y": 16, "t": 17, "1": 18, "2": 19, "3": 20, "4": 21,
    "6": 22, "5": 23, "=": 24, "9": 25, "7": 26, "-": 27, "8": 28, "0": 29, "]": 30, "o": 31,
    "u": 32, "[": 33, "i": 34, "p": 35, "l": 37, "j": 38, "'": 39, "k": 40, ";": 41, "\\": 42,
    ",": 43, "/": 44, "n": 45, "m": 46, ".": 47, "`": 50,
    "return": 36, "enter": 36, "tab": 48, "space": 49, "delete": 51, "backspace": 51, "esc": 53, "escape": 53,
    "fdelete": 117, "home": 115, "end": 119, "pageup": 116, "pagedown": 121,
    "left": 123, "right": 124, "down": 125, "up": 126,
    "f1": 122, "f2": 120, "f3": 99, "f4": 118, "f5": 96, "f6": 97, "f7": 98, "f8": 100,
    "f9": 101, "f10": 109, "f11": 103, "f12": 111,
]

let modFlags: [String: CGEventFlags] = [
    "cmd": .maskCommand, "command": .maskCommand, "shift": .maskShift,
    "alt": .maskAlternate, "opt": .maskAlternate, "option": .maskAlternate,
    "ctrl": .maskControl, "control": .maskControl, "fn": .maskSecondaryFn,
]

func key(_ name: String, _ mods: [String]) {
    guard let code = keyCodes[name.lowercased()] else { die("unknown key: \(name)") }
    var flags = CGEventFlags()
    for m in mods {
        guard let f = modFlags[m.lowercased()] else { die("unknown modifier: \(m)") }
        flags.insert(f)
    }
    for down in [true, false] {
        let e = CGEvent(keyboardEventSource: src, virtualKey: code, keyDown: down)
        e?.flags = flags
        post(e)
    }
}

func type(_ text: String) {
    for ch in text.utf16 {
        var c = ch
        for down in [true, false] {
            let e = CGEvent(keyboardEventSource: src, virtualKey: 0, keyDown: down)
            e?.keyboardSetUnicodeString(stringLength: 1, unicodeString: &c)
            post(e)
        }
    }
}

let a = Array(CommandLine.arguments.dropFirst())
guard let cmd = a.first else { die("usage: dc move|click|dclick|rclick|drag|scroll|type|key|pos ...") }

switch cmd {
case "move": mouse(.mouseMoved, pt(a, 1))
case "click": click(pt(a, 1))
case "dclick": click(pt(a, 1), count: 2)
case "rclick": click(pt(a, 1), .right)
case "drag":
    let p1 = pt(a, 1), p2 = pt(a, 3)
    mouse(.mouseMoved, p1)
    mouse(.leftMouseDown, p1)
    let steps = 20
    for i in 1...steps {
        let t = Double(i) / Double(steps)
        mouse(.leftMouseDragged, CGPoint(x: p1.x + (p2.x - p1.x) * t, y: p1.y + (p2.y - p1.y) * t))
    }
    mouse(.leftMouseUp, p2)
case "scroll":
    guard a.count > 1, let dy = Int32(a[1]) else { die("expected DY") }
    let dx = a.count > 2 ? Int32(a[2]) ?? 0 : 0
    post(CGEvent(scrollWheelEvent2Source: src, units: .line, wheelCount: 2, wheel1: dy, wheel2: dx, wheel3: 0))
case "type":
    guard a.count > 1 else { die("expected text") }
    type(a[1...].joined(separator: " "))
case "key":
    guard a.count > 1 else { die("expected key") }
    key(a[1], Array(a.dropFirst(2)))
case "pos":
    let p = CGEvent(source: nil)?.location ?? .zero
    print("\(Int(p.x)) \(Int(p.y))")
default:
    die("unknown command: \(cmd)")
}
