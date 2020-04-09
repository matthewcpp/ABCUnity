using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ABCUnity
{
    class VoiceLayout
    {
        public ABC.Voice voice { get; }

        public VoiceLayout(ABC.Voice v)
        {
            voice = v;
            alignment = new BeatAlignment(v);
            stafflineContainer = new GameObject();
        }

        /// <summary> updates the min and max values for current measure </summary>
        public void UpdateMeasureBounds(Bounds b)
        {
            if (b.min.y < measureMinY)
                measureMinY = b.min.y;

            if (b.max.y > measureMaxY)
                measureMaxY = b.max.y;
        }

        public void UpdateStaffBounds(Bounds b)
        {
            if (b.min.y < staffMinY)
                staffMinY = b.min.y;

            if (b.max.y > staffMaxY)
                staffMaxY = b.max.y;
        }

        public float height { get { return staffMaxY - staffMinY; } }

        /// <summary>Beat map for the voice.</summary>
        public BeatAlignment alignment { get; }

        /// <summary>The relative position from the origin to where the next measure will be inserted.</summary>
        public Vector3 staffPos = Vector3.zero;
        public float staffMinY { get; private set; } = float.MaxValue;
        public float staffMaxY { get; private set; } = float.MinValue;

        /// <summary>The index of the current beat that is active for this measure.</summary>
        public int beatAlignmentIndex { get; set; } = 0;

        /// <summary>The staff object on the current line.</summary>
        public GameObject currentStaff { get; set; }
        
        /// <summary>Game object which holds the staff and all measures on a current line.</summary>
        public GameObject stafflineContainer { get; set; }

        /// <summary>The parent game object that holds all items in the current measure.</summary>
        public GameObject measureContainer { get; set; }

        /// <summary>The relative position from the origin of the caret in the current measure.</summary>
        public Vector3 measurePos = Vector3.zero;

        public float measureMinY { get; private set; } = float.MaxValue;
        public float measureMaxY { get; private set; } = float.MinValue;

        /// <summary>Prepares this measure to render the next measure.</summary>
        public void NewMeasure()
        {
            beatAlignmentIndex = 0;
            measureContainer = new GameObject();
            measurePos = Vector3.zero;
            measureMinY = float.MaxValue;
            measureMaxY = float.MinValue;
        }

        public void NewStaffline()
        {
            staffPos = Vector3.zero;
            staffMinY = float.MaxValue;
            staffMaxY = float.MinValue;
            stafflineContainer = new GameObject();
        }

        public void UpdateStaffBounding()
        {
            staffMinY = Mathf.Min(staffMinY, measureMinY);
            staffMaxY = Mathf.Max(staffMaxY, measureMaxY);
        }
    }
}
