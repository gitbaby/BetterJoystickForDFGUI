using UnityEngine;

/// <summary>
/// Delegate for event <see cref="JoystickMovedEvent"/>
/// </summary>
/// <param name="dragDirection">Represents relative position of the joystick.
/// X and Y components are between 0 and 1. 0 is center and 1 is full radius.
/// Z component is always 0.</param>
public delegate void dfBetterJoystickMovedEventHandler (Vector3 dragDirection);

/// <summary>
/// Delegate for event <see cref="FingerTouchedEvent"/>
/// </summary>
public delegate void dfBetterJoystickFingerTouchedEventHandler ();

/// <summary>
/// Delegate for event <see cref="FingerLiftedEvent"/>
/// </summary>
public delegate void dfBetterJoystickFingerLiftedEventHandler ();

/// <summary>
/// Better joystick for DFGUI. Optimized for mobile devices. Based on Daikon Forge Joystick and Mobile CNJoystick.
/// </summary>
public class dfBetterJoystick : MonoBehaviour
{
		#region Public fields

		/// <summary>
		/// How far from center the thumb can move before being clamped.
		/// </summary>
		public int Radius = 90;

		/// <summary>
		/// Whether to move the thumb to the touched position on touch start.
		/// </summary>
		public bool IsDynamicThumb = false;

		/// <summary>
		/// Whether you are testing your game using Unity Remote app or not.
		/// </summary>
		public bool IsRemoteTesting = false;

		/// <summary>
		/// The thumb control.
		/// </summary>
		public dfControl ThumbControl;

		/// <summary>
		/// Visually represents the area inside of which the thumb control can be moved.
		/// </summary>
		public dfControl AreaControl;

		#endregion
	
		#region Script-only public variables

		/// <summary>
		/// Is called each frame if player is currently tweaking the joystick.
		/// </summary>
		public event dfBetterJoystickMovedEventHandler JoystickMovedEvent;

		/// <summary>
		/// Is called once when player begins tweaking the joystick.
		/// </summary>
		public event dfBetterJoystickFingerTouchedEventHandler FingerTouchedEvent;

		/// <summary>
		/// Is called once when user removes finger from the joystick.
		/// </summary>
		public event dfBetterJoystickFingerLiftedEventHandler FingerLiftedEvent;

		#endregion

		#region Private variables

		// Private delegate to automatically switch between Mouse input and Touch input
		private delegate void InputHandler ();

		/// <summary>
		/// The joystick control.
		/// </summary>
		private dfControl DFControl;

		/// <summary>
		/// The GUI manager.
		/// </summary>
		dfGUIManager DFGUIManager;
	
		/// <summary>
		/// The finger ID to track.
		/// </summary>
		private int MyFingerId = -1;
	
		/// <summary>
		/// The initial touch point in DFGUI screen coordinates.
		/// </summary>
		private Vector3 InvokeTouchPoint;

		/// <summary>
		/// Indicates that this joystick is currently being tweaked.
		/// </summary>
		private bool IsTweaking = false;

		/// <summary>
		/// The current input handler, <see cref="TouchInputHandler"/> or <see cref="MouseInputHandler"/>.
		/// </summary>
		private InputHandler CurrentInputHandler;

		#endregion

		#region Monobehavior events

		/// <summary>
		/// Initializes any variables or game state before the game starts.
		/// </summary>
		public void Awake ()
		{
				// Get joystick control
				DFControl = GetComponent<dfControl> ();
				// Get GUI manager
				DFGUIManager = DFControl.GetManager ();
				// Choose input handler
				#if UNITY_IPHONE || UNITY_ANDROID || UNITY_WP8 || UNITY_BLACKBERRY
				CurrentInputHandler = TouchInputHandler;
				#endif
				#if UNITY_EDITOR || UNITY_WEBPLAYER || UNITY_STANDALONE
				CurrentInputHandler = MouseInputHandler;
				#endif
				if (IsRemoteTesting) {
						CurrentInputHandler = TouchInputHandler;
				}
		}

		/// <summary>
		/// Is called on the frame when a script is enabled just before any of the Update methods is called the first time.
		/// </summary>
		public void Start ()
		{
				// Check configuration
				if (DFControl == null || ThumbControl == null || AreaControl == null) {
						Debug.LogError ("Invalid virtual joystick configuration", this);
						this.enabled = false;
						return;
				}
				// Set joystick to initial position
				ResetJoystick ();
		}

		/// <summary>
		/// Is called every frame.
		/// </summary>
		public void Update ()
		{
				// Automatically call proper input handler
				CurrentInputHandler ();
		}

		#endregion

		#region Input handlers

		/// <summary>
		/// The touch input handler. Most of the work is done here.
		/// </summary>
		private void TouchInputHandler ()
		{
				// Current touch count
				int touchCount = Input.touchCount;
				// If we're not yet tweaking, we should check
				// whether any touch lands on our BoxCollider or not
				if (!IsTweaking) {
						for (int i = 0; i < touchCount; i++) {
								// Get current touch
								Touch touch = Input.GetTouch (i);
								// We check it's phase.
								// If it's not a Begin phase, finger didn't tap the screen
								// it's probably just slided to our TapRect
								// So for the sake of optimization we won't do anything with this touch
								// But if it's a tap, we check if it lands on our TapRect 
								// See TouchOccured function
								if (touch.phase == TouchPhase.Began && TouchOccured (touch.position)) {
										// We should store our finger ID 
										MyFingerId = touch.fingerId;
										// If it's a valid touch, we dispatch our FingerTouchEvent
										if (FingerTouchedEvent != null) {
												FingerTouchedEvent ();
										}
										// Start tweaking
										StartTweakingJoystick (touch.position);
								}
						}
				} else {
						// We take Touch screen position and convert it to local joystick - relative coordinates
						// This boolean represents if current touch has a Ended phase.
						// It's here for more code readability
						bool isGoingToEnd = false;
						for (int i = 0; i < touchCount; i++) {
								Touch touch = Input.GetTouch (i);
								// For every finger out there, we check if OUR finger has just lifted from the screen
								if (MyFingerId == touch.fingerId && touch.phase == TouchPhase.Ended) {
										// And if it does, we reset our Joystick with this function
										ResetJoystick ();
										// We store our boolean here
										isGoingToEnd = true;
										// And dispatch our FingerLiftedEvent
										if (FingerLiftedEvent != null) {
												FingerLiftedEvent ();
										}
								}
						}
						// If user didn't lift his finger this frame
						if (!isGoingToEnd) {
								// We find our current Touch index (it's not always equal to Finger index)
								int currentTouchIndex = FindMyFingerId ();
								if (currentTouchIndex != -1) {
										// And call our TweakJoystick function with this finger
										TweakJoystick (Input.GetTouch (currentTouchIndex).position);
								}
						}
				}
		}

		#if UNITY_EDITOR || UNITY_WEBPLAYER || UNITY_STANDALONE

		/// <summary>
		/// The mouse input handler.
		/// </summary>
		private void MouseInputHandler ()
		{
				if (Input.GetMouseButtonDown (0) && TouchOccured (Input.mousePosition)) {
						StartTweakingJoystick (Input.mousePosition);
				}
				if (IsTweaking && Input.GetMouseButton (0)) {
						TweakJoystick (Input.mousePosition);
				}
				if (Input.GetMouseButtonUp (0)) {
						ResetJoystick ();
				}
		}

		#endif

		/// <summary>
		/// Checks if this joystick was touched.
		/// </summary>
		/// <returns><c>true</c>, if touch occured, <c>false</c> otherwise.</returns>
		/// <param name="touchPosition">Touch position.</param>
		private bool TouchOccured (Vector3 touchPosition)
		{
				// Get rectangle containing this joystick in screen-based coordinates
				Rect dfControlRect = DFControl.GetScreenRect ();
				dfControlRect.y = Screen.height - dfControlRect.y - dfControlRect.height;
				// Check if touch occured inside this joystick panel
				return dfControlRect.Contains (touchPosition);
		}

		/// <summary>
		/// Starts tweaking joystick after touch occured.
		/// </summary>
		/// <param name="touchPosition">Touch position.</param>
		private void StartTweakingJoystick (Vector3 touchPosition)
		{
				if (IsDynamicThumb) {
						// Save initial touch position in DFGUI screen coordinates
						InvokeTouchPoint = ToScreenCoord (touchPosition);
						// Move joystick to touch position
						MoveJoystick (InvokeTouchPoint);
				} else {
						// Get joystick center
						Rect dfControlRect = DFControl.GetScreenRect ();
						Vector3 dfControlCenter = new Vector3 (dfControlRect.x + dfControlRect.width * 0.5f,
			                                       Screen.height - dfControlRect.y - dfControlRect.height * 0.5f,
			                                       0f);
						// Set initial touch position as if it was joystick center
						InvokeTouchPoint = ToScreenCoord (dfControlCenter);
						// Tweak now, don't wait for the next frame
						TweakJoystick (touchPosition);
				}
				// Start tweaking
				IsTweaking = true;
		}
	
		/// <summary>
		/// Try to drag small joystick knob to it's desired position.
		/// </summary>
		/// <param name="touchPosition">Touch position.</param>
		private void TweakJoystick (Vector3 touchPosition)
		{
				// Convert touch position to DFGUI screen coordinates
				Vector3 touchPoint = ToScreenCoord (touchPosition);
				// Move joystick to touch point
				MoveJoystick (touchPoint);
				// Raise JoystickMovedEvent
				if (JoystickMovedEvent != null) {
						// Find our joystick drag direction
						Vector3 dragDirection = touchPoint - InvokeTouchPoint;
						// If the joystick is inside it's base, we keep it's position under the finger
						// sqrMagnitude is used for optimization, as Multiplication operation is much cheaper than Square Root
						if (dragDirection.sqrMagnitude <= Radius * Radius) {
								dragDirection /= Radius;
						} else {
								// But if we drag our finger too far, joystick will remain at the border of it's base
								dragDirection.Normalize ();
						}
						// Dispatch our event
						JoystickMovedEvent (dragDirection);
				}
		}
	
		#endregion
	
		#region Private utility methods

		/// <summary>
		/// Converts point to DFGUI screen coordinates.
		/// </summary>
		/// <returns>The point in DFGUI screen coordinates.</returns>
		/// <param name="point">Touch or mouse position.</param>
		private Vector3 ToScreenCoord (Vector3 point)
		{
				// Get screen size
				Vector2 screenSize = DFGUIManager.GetScreenSize ();
				// Convert point
				return new Vector3 (point.x * screenSize.x / Screen.width,
		                    point.y * screenSize.y / Screen.height,
		                    0f);
		}
	
		/// <summary>
		/// Moves joystick to the specified point on screen.
		/// </summary>
		/// <param name="toPoint">Point in DFGUI screen coordinates.</param>
		private void MoveJoystick (Vector3 toPoint)
		{
				// Get point relative to the joystick panel
				Vector2 relativePoint = new Vector2 (toPoint.x - DFControl.RelativePosition.x,
		                                     DFGUIManager.GetScreenSize ().y - toPoint.y - DFControl.RelativePosition.y);
				// If dynamic thumb, on first touch center thumb around touch position
				if (!IsTweaking && IsDynamicThumb) {
						// Center thumb area around touch position
						AreaControl.RelativePosition = relativePoint - AreaControl.Size * 0.5f;
						// Center thumb in area
						ThumbControl.RelativePosition = AreaControl.RelativePosition + (Vector3)(AreaControl.Size - ThumbControl.Size) * 0.5f;
				}
				// Get point local to thumb area center
				Vector3 areaCenter = AreaControl.RelativePosition + (Vector3)AreaControl.Size * 0.5f;
				Vector3 localPoint = (Vector3)relativePoint - areaCenter;
				// Clamp to radius
				if (localPoint.sqrMagnitude > Radius * Radius) {
						localPoint = localPoint.normalized * Radius;
				}
				// Set thumb position
				Vector3 thumbCenter = (Vector3)ThumbControl.Size * 0.5f;
				ThumbControl.RelativePosition = areaCenter - thumbCenter + localPoint;
		}
	
		/// <summary>
		/// Resets the joystick to its initial position.
		/// </summary>
		private void ResetJoystick ()
		{
				IsTweaking = false;
				AreaControl.RelativePosition = (DFControl.Size - AreaControl.Size) * 0.5f;
				Vector3 areaCenter = AreaControl.RelativePosition + (Vector3)AreaControl.Size * 0.5f;
				Vector3 thumbCenter = (Vector3)ThumbControl.Size * 0.5f;
				ThumbControl.RelativePosition = areaCenter - thumbCenter;
				MyFingerId = -1;
		}

		/// <summary>
		/// Sometimes when user lifts his finger, current touch index changes.
		/// To keep track of our finger, we need to know which finger has the user lifted.
		/// </summary>
		/// <returns>Finger ID.</returns>
		private int FindMyFingerId ()
		{
				int touchCount = Input.touchCount;
				for (int i = 0; i < touchCount; i++) {
						if (Input.GetTouch (i).fingerId == MyFingerId) {
								// We return current Touch index if it's our finger
								return i;
						}
				}
				// And we return -1 if there's no such finger
				// Usually this happend after user lifts the finger which he touched first
				return -1;
		}

		#endregion
}
