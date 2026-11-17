using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AssemblyWorkshop.Common
{
    /// <summary>
    /// Pointer helpers shared by mouse and touch input.
    /// </summary>
    public static class PointerUtility
    {
        private static readonly List<RaycastResult> Results = new List<RaycastResult>();
        private static PointerEventData eventData;
        private static EventSystem eventDataOwner;

        /// <summary>
        /// Whether a UI element that receives raycasts is under the screen position.
        /// Raycasts the UI directly instead of using IsPointerOverGameObject, which can be one frame late for touches.
        /// </summary>
        public static bool IsOverUI(Vector2 screenPosition)
        {
            EventSystem current = EventSystem.current;
            if (current == null)
            {
                return false;
            }

            if (eventData == null || eventDataOwner != current)
            {
                eventData = new PointerEventData(current);
                eventDataOwner = current;
            }

            eventData.position = screenPosition;
            Results.Clear();
            current.RaycastAll(eventData, Results);
            return Results.Count > 0;
        }

        /// <summary>
        /// Number of fingers currently touching the scene. Fingers that started on the UI are not counted,
        /// so holding a piece with one finger and pressing a button with another is not a two-finger gesture.
        /// </summary>
        public static int SceneTouchCount()
        {
            return GetSceneTouches(null);
        }

        /// <summary>
        /// Fills 'touches' (if not null) with the fingers touching the scene, in order, and returns how many there are.
        /// </summary>
        public static int GetSceneTouches(List<TouchControl> touches)
        {
            touches?.Clear();

            Touchscreen screen = Touchscreen.current;
            if (screen == null)
            {
                return 0;
            }

            int count = 0;
            foreach (TouchControl touch in screen.touches)
            {
                // The start position decides it: a finger that began on a button stays a UI finger while it moves
                if (!touch.isInProgress || IsOverUI(touch.startPosition.ReadValue()))
                {
                    continue;
                }

                touches?.Add(touch);
                count++;
            }

            return count;
        }
    }
}