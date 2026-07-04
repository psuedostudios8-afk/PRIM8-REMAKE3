namespace StarLight.Inputs
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Unity.XR.Oculus;
    using UnityEngine;
    using UnityEngine.XR;

    public enum ButtonCode
    {
        PrimaryButtonTouched,
        SecondaryButtonTouched,
        ThumbStickTouched,
        SecondaryButton,
        TriggerTouched,
        PrimaryButton,
        ThumbStick,
        MenuButton,
        Trigger,
        Grip
    }

    public enum AxisCode
    {
        ThumbStickHorizontal,
        ThumbStickVertical,
        Trigger,
        Grip
    }

    public enum TransformData
    {
        Rotation,
        Position,
        Velocity,
        AngularVelocity,
        Acceleration,
        AngularAcceleration
    }

    public enum StarLightHand
    {
        LeftHand = 4,
        RightHand = 5
    }

    public enum StarLightDevice
    {
        HMD = 3,
        LeftHand = 4,
        RightHand = 5
    }

    public static partial class StarLightInputs
    {
        private static Dictionary<ButtonCode, Dictionary<StarLightHand, bool>> buttonDownStateDict = new Dictionary<ButtonCode, Dictionary<StarLightHand, bool>>();
        private static Dictionary<ButtonCode, Dictionary<StarLightHand, bool>> buttonUpStateDict = new Dictionary<ButtonCode, Dictionary<StarLightHand, bool>>();

        /// <summary>
        /// Get's If The Button Is true of false.
        /// </summary>
        public static bool GetButton(ButtonCode button, StarLightHand hand)
        {
            switch (button)
            {
                case ButtonCode.PrimaryButtonTouched:
                    return GetInputBool(hand, CommonUsages.primaryTouch);

                case ButtonCode.SecondaryButtonTouched:
                    return GetInputBool(hand, CommonUsages.secondaryTouch);

                case ButtonCode.ThumbStickTouched:
                    return GetInputBool(hand, CommonUsages.primary2DAxisTouch);

                case ButtonCode.SecondaryButton:
                    return GetInputBool(hand, CommonUsages.secondaryButton);

                case ButtonCode.TriggerTouched:
                    return GetInputBool(hand, OculusUsages.indexTouch);

                case ButtonCode.PrimaryButton:
                    return GetInputBool(hand, CommonUsages.primaryButton);

                case ButtonCode.ThumbStick:
                    return GetInputBool(hand, CommonUsages.primary2DAxisClick);

                case ButtonCode.MenuButton:
                    return GetInputBool(hand, CommonUsages.menuButton);

                case ButtonCode.Trigger:
                    return GetInputBool(hand, CommonUsages.triggerButton);

                case ButtonCode.Grip:
                    return GetInputBool(hand, CommonUsages.gripButton);
            }
            return false;
        }

        public static bool GetButtonDown(ButtonCode button, StarLightHand hand)
        {
            bool isPressed = GetButton(button, hand);
            if (buttonDownStateDict.TryGetValue(button, out Dictionary<StarLightHand, bool> handDict))
            {
                if (handDict.TryGetValue(hand, out bool prevState))
                {
                    bool result = isPressed && prevState == false;
                    handDict[hand] = isPressed;
                    return result;
                }
                else
                {
                    handDict[hand] = isPressed;
                    return false;
                }
            }
            else
            {
                buttonDownStateDict[button] = new Dictionary<StarLightHand, bool>();
                buttonDownStateDict[button][hand] = isPressed;
                return false;
            }
        }

        public static bool GetButtonUp(ButtonCode button, StarLightHand hand)
        {
            bool isPressed = GetButton(button, hand);
            if (buttonUpStateDict.TryGetValue(button, out Dictionary<StarLightHand, bool> handDict))
            {
                if (handDict.TryGetValue(hand, out bool prevState))
                {
                    bool result = !isPressed && prevState;
                    handDict[hand] = isPressed;
                    return result;
                }
                else
                {
                    handDict[hand] = isPressed;
                    return false;
                }
            }
            else
            {
                buttonUpStateDict[button] = new Dictionary<StarLightHand, bool>();
                buttonUpStateDict[button][hand] = isPressed;
                return false;
            }
        }



        /// <summary>
        /// Get's The Axis.
        /// </summary>
        public static float GetAxis(AxisCode Axis, StarLightHand hand)
        {
            switch (Axis)
            {
                case AxisCode.Trigger:
                    return GetInputAxis(hand, CommonUsages.trigger);

                case AxisCode.Grip:
                    return GetInputAxis(hand, CommonUsages.grip);

                case AxisCode.ThumbStickHorizontal:
                    return GetInputVector2(hand, CommonUsages.primary2DAxis).x;

                case AxisCode.ThumbStickVertical:
                    return GetInputVector2(hand, CommonUsages.primary2DAxis).y;

            }

            return 0;
        }

        /// <summary>
        /// Get's The Axis.
        /// </summary>
        public static float GetAxisRaw(AxisCode Axis, StarLightHand hand)
        {
            switch (Axis)
            {
                case AxisCode.Trigger:
                    return GetInputAxis(hand, CommonUsages.trigger) / 100;

                case AxisCode.Grip:
                    return GetInputAxis(hand, CommonUsages.grip) / 100;

                case AxisCode.ThumbStickHorizontal:
                    return GetInputVector2(hand, CommonUsages.primary2DAxis).x / 100;

                case AxisCode.ThumbStickVertical:
                    return GetInputVector2(hand, CommonUsages.primary2DAxis).y / 100;

            }

            return 0;
        }

        /// <summary>
        /// Get's The Transform Data
        /// </summary>
        public static T GetTransformsData<T>(TransformData transforms, StarLightDevice device)
        {
            T value = default(T);

            switch (transforms)
            {
                case TransformData.Position:
                    value = (T)(object)(GetInputVector3(device, CommonUsages.devicePosition));
                    break;

                case TransformData.Velocity:
                    value = (T)(object)(GetInputVector3(device, CommonUsages.deviceVelocity));
                    break;

                case TransformData.AngularVelocity:
                    value = (T)(object)(GetInputVector3(device, CommonUsages.deviceAngularVelocity));
                    break;

                case TransformData.AngularAcceleration:
                    value = (T)(object)(GetInputVector3(device, CommonUsages.deviceAngularAcceleration));
                    break;

                case TransformData.Acceleration:
                    value = (T)(object)(GetInputVector3(device, CommonUsages.deviceAcceleration));
                    break;

                case TransformData.Rotation:
                    value = (T)(object)(GetInputQuaternion(device, CommonUsages.deviceRotation));
                    break;
            }

            return value;
        }

        /// <summary>
        /// Vibrates The Hand
        /// </summary>
        public static void VibrateHand(StarLightHand hand, float Amplitude, float Duration, uint channel = 0u)
        {
            if (InputDevices.GetDeviceAtXRNode((XRNode)hand).TryGetHapticCapabilities(out HapticCapabilities capabilities))
            {
                InputDevices.GetDeviceAtXRNode((XRNode)hand).SendHapticImpulse(channel, Amplitude, Duration);
            }
        }


        static bool GetInputBool(StarLightHand hand, InputFeatureUsage<bool> usage)
        {
            InputDevices.GetDeviceAtXRNode((XRNode)hand).TryGetFeatureValue(usage, out bool value);
            return value;
        }

        static float GetInputAxis(StarLightHand hand, InputFeatureUsage<float> usage)
        {
            InputDevices.GetDeviceAtXRNode((XRNode)hand).TryGetFeatureValue(usage, out float value);
            return value * 100;
        }

        static Vector2 GetInputVector2(StarLightHand hand, InputFeatureUsage<Vector2> usage)
        {
            InputDevices.GetDeviceAtXRNode((XRNode)hand).TryGetFeatureValue(usage, out Vector2 value);
            return value * 100;
        }

        static Vector3 GetInputVector3(StarLightDevice device, InputFeatureUsage<Vector3> usage)
        {
            InputDevices.GetDeviceAtXRNode((XRNode)device).TryGetFeatureValue(usage, out Vector3 value);
            return value;
        }

        static Quaternion GetInputQuaternion(StarLightDevice device, InputFeatureUsage<Quaternion> usage)
        {
            InputDevices.GetDeviceAtXRNode((XRNode)device).TryGetFeatureValue(usage, out Quaternion value);
            return value;
        }
    }
}