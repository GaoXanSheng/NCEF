using System.Windows.Input;

namespace NCEF_Test.TestCases
{
    public static class InputHelper
    {
        public static int GetVirtualKeyCode(Key key)
        {
            if (key >= Key.A && key <= Key.Z) return 0x41 + (key - Key.A);
            if (key >= Key.D0 && key <= Key.D9) return 0x30 + (key - Key.D0);
            if (key >= Key.F1 && key <= Key.F12) return 0x70 + (key - Key.F1);

            return key switch
            {
                Key.Back => 0x08,
                Key.Tab => 0x09,
                Key.Return => 0x0D,
                Key.Escape => 0x1B,
                Key.Space => 0x20,
                Key.Insert => 0x2D,
                Key.Delete => 0x2E,
                Key.Home => 0x24,
                Key.End => 0x23,
                Key.PageUp => 0x21,
                Key.PageDown => 0x22,
                Key.Left => 0x25,
                Key.Up => 0x26,
                Key.Right => 0x27,
                Key.Down => 0x28,
                Key.LeftShift or Key.RightShift => 0x10,
                Key.LeftCtrl or Key.RightCtrl => 0x11,
                Key.LeftAlt or Key.RightAlt => 0x12,
                _ => 0
            };
        }

        public static int GetCurrentModifiers()
        {
            int modifiers = 0;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) modifiers |= 1;
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) modifiers |= 2;
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) modifiers |= 4;
            return modifiers;
        }
    }
}
