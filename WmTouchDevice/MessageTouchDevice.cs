﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WmTouchDevice.Native;

namespace WmTouchDevice
{
    public class MessageTouchDevice : TouchDevice
    {
        private static DateTime _lastTouch;
        public static DateTime LastTouch
        {
            get { return _devices.Count != 0 ? DateTime.Now : _lastTouch; }
        }

        private readonly static Dictionary<int, MessageTouchDevice> _devices = new Dictionary<int, MessageTouchDevice>();

        public static void RegisterTouchWindow(IntPtr hWnd)
        {
            TabletHelper.DisableWPFTabletSupport(hWnd);
            NativeMethods.RegisterTouchWindow(hWnd, NativeMethods.TWF_WANTPALM);
        }

        public static void WndProc(Window window, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_TOUCH)
            {
                var inputCount = wParam.ToInt32() & 0xffff;
                var inputs = new TOUCHINPUT[inputCount];

                if (NativeMethods.GetTouchInputInfo(lParam, inputCount, inputs, NativeMethods.TouchInputSize))
                {
                    for (int i = 0; i < inputCount; i++)
                    {
                        var input = inputs[i];
                        var position = GraphicsHelper.DivideByDpi(new Point(input.x * 0.01, input.y * 0.01));
                        position = window.PointFromScreen(position);

                        MessageTouchDevice device;
                        if (!_devices.TryGetValue(input.dwID, out device))
                        {
                            device = new MessageTouchDevice(input.dwID);
                            _devices.Add(input.dwID, device);
                        }

                        if (!device.IsActive && input.dwFlags.HasFlag(TOUCHEVENTF.TOUCHEVENTF_DOWN))
                        {
                            device.SetActiveSource(PresentationSource.FromVisual(window));
                            device.Position = position;
                            device.Activate();
                            device.ReportDown();
                        }
                        else if (device.IsActive && input.dwFlags.HasFlag(TOUCHEVENTF.TOUCHEVENTF_UP))
                        {
                            device.Position = position;
                            device.ReportUp();
                            device.Deactivate();
                            _devices.Remove(input.dwID);
                        }
                        else if (device.IsActive && input.dwFlags.HasFlag(TOUCHEVENTF.TOUCHEVENTF_MOVE) && device.Position != position)
                        {
                            device.Position = position;
                            device.ReportMove();
                        }

                        _lastTouch = DateTime.Now;
                    }
                }

                NativeMethods.CloseTouchInputHandle(lParam);
                handled = true;
            }
        }

        internal MessageTouchDevice(int id)
            : base(id) { }

        public Point Position { get; set; }

        public override TouchPointCollection GetIntermediateTouchPoints(IInputElement relativeTo)
        {
            return new TouchPointCollection();
        }

        public override TouchPoint GetTouchPoint(IInputElement relativeTo)
        {
            Point pt = Position;
            if (relativeTo != null)
            {
                var rootVisual = this.ActiveSource.RootVisual;
                var relativeVisual = (Visual)relativeTo;

                if (rootVisual.IsAncestorOf(relativeVisual))
                    pt = rootVisual.TransformToDescendant(relativeVisual).Transform(Position);
            }

            var rect = new Rect(pt, new Size(1.0, 1.0));
            return new TouchPoint(this, pt, rect, TouchAction.Move);
        }

        protected override void OnCapture(IInputElement element, CaptureMode captureMode)
        {
            Mouse.PrimaryDevice.Capture(element, captureMode);
        }
    }
}
