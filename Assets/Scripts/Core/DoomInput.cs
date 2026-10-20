using UnityEngine;
using UnityEngine.InputSystem;

namespace Doom
{
    /// <summary>
    /// All input actions, defined in code so there is no asset to keep in sync.
    /// Keyboard + mouse and any gamepad (XInput / DualShock / DualSense / Switch Pro) are bound.
    /// </summary>
    public static class DoomInput
    {
        public static InputAction Move, KeyTurn, MouseLook, StickLook;
        public static InputAction Fire, Use, Run, NextWeapon, PrevWeapon, Automap, Pause;
        public static InputAction MenuUp, MenuDown, MenuLeft, MenuRight, MenuConfirm, MenuBack;
        public static readonly InputAction[] Slots = new InputAction[7];

        static bool initialized;

        public static void Init()
        {
            if (initialized) return;
            initialized = true;

            Move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a").With("Left", "<Keyboard>/comma")
                .With("Right", "<Keyboard>/d").With("Right", "<Keyboard>/period");
            Move.AddBinding("<Gamepad>/leftStick");

            KeyTurn = new InputAction("KeyTurn", InputActionType.Value, expectedControlType: "Axis");
            KeyTurn.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");

            MouseLook = new InputAction("MouseLook", InputActionType.PassThrough, "<Mouse>/delta");
            StickLook = new InputAction("StickLook", InputActionType.Value, "<Gamepad>/rightStick");

            Fire = Button("Fire", "<Mouse>/leftButton", "<Keyboard>/leftCtrl", "<Keyboard>/rightCtrl", "<Gamepad>/rightTrigger");
            Use = Button("Use", "<Keyboard>/e", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Run = Button("Run", "<Keyboard>/leftShift", "<Keyboard>/rightShift");
            NextWeapon = Button("NextWeapon", "<Keyboard>/rightBracket", "<Gamepad>/rightShoulder", "<Gamepad>/buttonNorth");
            PrevWeapon = Button("PrevWeapon", "<Keyboard>/q", "<Keyboard>/leftBracket", "<Gamepad>/leftShoulder");
            Automap = Button("Automap", "<Keyboard>/tab", "<Gamepad>/select");
            Pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");

            for (int i = 0; i < Slots.Length; i++)
                Slots[i] = Button("Slot" + (i + 1), "<Keyboard>/" + (i + 1), "<Keyboard>/numpad" + (i + 1));

            MenuUp = Button("MenuUp", "<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up", "<Gamepad>/leftStick/up");
            MenuDown = Button("MenuDown", "<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down");
            MenuLeft = Button("MenuLeft", "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
            MenuRight = Button("MenuRight", "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");
            MenuConfirm = Button("MenuConfirm", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            MenuBack = Button("MenuBack", "<Keyboard>/escape", "<Keyboard>/backspace", "<Gamepad>/buttonEast");

            foreach (var a in new[] { Move, KeyTurn, MouseLook, StickLook, Fire, Use, Run, NextWeapon, PrevWeapon,
                         Automap, Pause, MenuUp, MenuDown, MenuLeft, MenuRight, MenuConfirm, MenuBack })
                a.Enable();
            foreach (var s in Slots) s.Enable();
        }

        static InputAction Button(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) a.AddBinding(b);
            return a;
        }

        /// <summary>True when the last move input came from a gamepad stick (analog speed control).</summary>
        public static bool MoveIsAnalog => Move.activeControl != null && Move.activeControl.device is Gamepad;

        public static float MouseWheel => Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;

        public static bool AnyAdvance =>
            MenuConfirm.WasPressedThisFrame() || Fire.WasPressedThisFrame() || Use.WasPressedThisFrame();
    }
}
