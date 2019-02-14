﻿#if XNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#endif

using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using SadConsole.DrawCalls;
using SadConsole.Input;
using SadConsole.Themes;
using Keyboard = SadConsole.Input.Keyboard;
using Mouse = SadConsole.Input.Mouse;

namespace SadConsole
{
    /// <summary>
    /// Represents a windowed controls console.
    /// </summary>
    [DataContract]
    [DebuggerDisplay("Window")]
    public partial class Window: ControlsConsole
    {
        /// <summary>
        /// Raised when the window is closed.
        /// </summary>
        public event EventHandler Closed;

        [DataMember(Name="Title")]
        private string _title;

        [DataMember(Name="Theme")]
        private Library _theme;
        
        [DataMember(Name="TitleAlignment")]
        private HorizontalAlignment _titleAlignment;

        /// <summary>
        /// The mouse state of the previous update frame.
        /// </summary>
        protected MouseConsoleState PreviousMouseInfo = new MouseConsoleState(null, new Mouse());

        /// <summary>
        /// The position of the cell that the window drag started at.
        /// </summary>
        protected Point CellAtDragPosition;

        /// <summary>
        /// Temporary value to hold the state of <see cref="Console.IsExclusiveMouse"/> prior to dragging.
        /// </summary>
        protected bool PreviousMouseExclusiveDrag;

        /// <summary>
        /// When <see langword="true"/>, indicates that the window has been added to a parent; otherwise <see langword="false"/>.
        /// </summary>
        protected bool AddedToParent;

        /// <summary>
        /// When <see langword="true"/>, indicates that the window is being dragged; otherwise <see langword="false"/>.
        /// </summary>
        protected bool IsDragging;

        /// <summary>
        /// Gets or sets the alignment of the window title.
        /// </summary>
        [DataMember]
        public HorizontalAlignment TitleAlignment
        {
            get => _titleAlignment;
            set
            {
                _titleAlignment = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets the whether or not the console is being shown as modal. 
        /// </summary>
        public bool IsModal { get; private set; }

        /// <summary>
        /// Gets or sets whether or not this window can be moved with the mouse.
        /// </summary>
        [DataMember]
        public bool CanDrag { get; set; }
        
        /// <summary>
        /// Gets or sets whether or not this window can be closed when the escape key is pressed.
        /// </summary>
        [DataMember]
        public bool CloseOnEscKey { get; set; }
        
        /// <summary>
        /// Gets or set the dialog result status of the window.
        /// </summary>
        [DataMember]
        public bool DialogResult { get; set; }
        
        /// <summary>
        /// Indicates that when this window is shown by the <see cref="Show()"/> method or by setting the <see cref="Console.IsVisible"/> property to true, the window will be shown as modal.
        /// </summary>
        [DataMember]
        public bool IsModalDefault { get; set; }

        /// <summary>
        /// Gets or sets the title displayed on the window.
        /// </summary>
        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the theme of the window.
        /// </summary>
        public new Library Theme
        {
            get => _theme ?? Library.Default;
            set
            {
                _theme = value ?? throw new ArgumentNullException(nameof(Theme), "Theme cannot be set to null.");

                foreach (var control in Controls)
                    control.RefreshParentTheme();

                IsDirty = true;
                Invalidate();
            }
        }
        
        /// <summary>
        /// Creates a new window with the specified with and height in cells.
        /// </summary>
        /// <param name="width">The width of the window in cells.</param>
        /// <param name="height">The height of the window in cells.</param>
        public Window(int width, int height): this(width, height, Global.FontDefault) { }

        /// <summary>
        /// Creates a new window with the specified with and height in cells.
        /// </summary>
        /// <param name="width">The width of the window in cells.</param>
        /// <param name="height">The height of the window in cells.</param>
        /// <param name="font">THe font to use with the window.</param>
        public Window(int width, int height, Font font): base(width, height, font)
        {
            IsVisible = false;
            CanDrag = true;
            MoveToFrontOnMouseClick = true;
        }

        /// <summary>
        /// Depending on if the window is visible, calls <see cref="Show(bool)"/> or <see cref="Hide"/>.
        /// </summary>
        protected override void OnVisibleChanged()
        {
            base.OnVisibleChanged();

            if (IsVisible)
                Show(IsModalDefault);
            else
                Hide();
        }

        /// <inheritdoc />
        public override void Draw(TimeSpan drawTime)
        {
            if (IsModal && Theme.WindowTheme.ModalTint.A != 0)
                Global.DrawCalls.Add(new DrawCallColoredRect(new Rectangle(0, 0, Global.RenderWidth, Global.RenderHeight), Theme.WindowTheme.ModalTint));

            base.Draw(drawTime);
        }

        /// <summary>
        /// Causes the window to be redrawn with the selected <see cref="Theme"/>.
        /// </summary>
        public new virtual void Invalidate()
        {
            Theme.WindowTheme.Refresh(Theme.Colors);
            Theme.WindowTheme.Draw(this, this);
            IsDirty = true;

            foreach (var control in ControlsList)
                control.IsDirty = true;
        }

        /// <inheritdoc />
        public override bool ProcessMouse(MouseConsoleState state)
        {
            if (!CanDrag || Theme.WindowTheme.TitleAreaLength == 0 || !IsVisible)
            {
                PreviousMouseInfo = state;
                return base.ProcessMouse(state);
            }

            if (IsDragging && state.Mouse.LeftButtonDown)
            {
                if (state.Mouse.IsOnScreen)
                {
                    Position = state.WorldPosition - CellAtDragPosition;
                    PreviousMouseInfo = state;
                    return true;
                }
            }

            // Stopped dragging
            if (IsDragging && !state.Mouse.LeftButtonDown)
            {
                IsDragging = false;
                IsExclusiveMouse = PreviousMouseExclusiveDrag;
                PreviousMouseInfo = state;
                return true;
            }

            // Left button freshly down and we're not already dragging, check to see if in title
            if (CapturedControl == null && state.IsOnConsole && !IsDragging && !PreviousMouseInfo.Mouse.LeftButtonDown && state.Mouse.LeftButtonDown)
            {
                if (state.CellPosition.Y == Theme.WindowTheme.TitleAreaY && state.CellPosition.X >= Theme.WindowTheme.TitleAreaX && state.CellPosition.X < Theme.WindowTheme.TitleAreaX + Theme.WindowTheme.TitleAreaLength)
                {
                    PreviousMouseExclusiveDrag = IsExclusiveMouse;

                    // Mouse is in the title bar
                    IsExclusiveMouse = true;
                    IsDragging = true;

                    if (UsePixelPositioning)
                        CellAtDragPosition = state.RelativePixelPosition;
                    else
                        CellAtDragPosition = state.ConsolePosition;

                    if (MoveToFrontOnMouseClick)
                    {
                        IsFocused = true;
                    }
                }
            }

            PreviousMouseInfo = state;
            return base.ProcessMouse(state);
        }

        /// <summary>
        /// Processes the keyboard looking for the ESC key press to close the console, if required. Otherwise the base ControlsConsole will process the keyboard.
        /// </summary>
        /// <param name="info">Keyboard state.</param>
        public override bool ProcessKeyboard(Keyboard info)
        {
            if (CloseOnEscKey && info.IsKeyReleased(Keys.Escape))
            {
                Hide();
                return true;
            }
            
            return base.ProcessKeyboard(info);
        }

        /// <summary>
        /// Displays this window using the modal value of the <see cref="IsModalDefault"/> property.
        /// </summary>
        public void Show()
        {
            Show(IsModalDefault);
        }

        /// <summary>
        /// Displays this window.
        /// </summary>
        /// <param name="modal">When true, the window will be displayed as modal; otherwise false.</param>
        public virtual void Show(bool modal)
        {
            if (Parent != null && IsVisible)
            {
                Parent.Children.MoveToTop(this);
                return;
            }

            if (IsVisible)
                return;
            IsVisible = true;

            IsExclusiveMouse = IsModal = modal;
            AddedToParent = false;

            if (Parent == null)
            {
                Parent = Global.CurrentScreen;
                AddedToParent = true;
            }

            Parent.Children.MoveToTop(this);

            if (modal)
            {
                Global.FocusedConsoles.Push(this);
                IsFocused = true;
                Global.MouseState.ClearLastMouseConsole();
            }
        }

        /// <summary>
        /// Hides the window.
        /// </summary>
        public virtual void Hide()
        {
            IsVisible = false;
            IsExclusiveMouse = false;

            if (IsModal)
                Global.FocusedConsoles.Pop(this);

            if (AddedToParent && Parent != null)
                Parent = null;

            Closed?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Centers the window within the bounds of <see cref="Global.RenderWidth"/> and <see cref="Global.RenderHeight"/>
        /// </summary>
        public void Center()
        {
            var screenWidth = Global.RenderWidth;
            var screenHeight = Global.RenderHeight;

            if (UsePixelPositioning)
                Position = new Point((screenWidth / 2) - ((Width * Font.Size.X) / 2), (screenHeight / 2) - ((Height * Font.Size.Y) / 2));
            else
                Position = new Point(((screenWidth / Font.Size.X) / 2) - (Width / 2), ((screenHeight / Font.Size.Y) / 2) - (Height / 2));
            
        }

        [OnDeserialized]
        private void AfterDeserialized(StreamingContext context)
        {
            PreviousMouseInfo = new MouseConsoleState(null, new Mouse());
            //Redraw();
        }
    }
}
