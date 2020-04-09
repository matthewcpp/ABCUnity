using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ABCUnity
{
    class VoiceLayout
    {
        public class Metrics
        {
            public float minY { get; set; } = float.MaxValue;
            public float maxY { get; set; } = float.MinValue;

            public float height { get { return maxY - minY; } }

            public void UpdateBounds(Bounds b)
            {
                minY = Mathf.Min(minY, b.min.y);
                maxY = Mathf.Max(maxY, b.max.y);
            }

            public Vector3 position = Vector3.zero;

            public GameObject container;
        }

        public ABC.Voice voice { get; }

        public Metrics staff { get; private set; }
        public Metrics measure { get; private set; }
        

        public VoiceLayout(ABC.Voice v)
        {
            voice = v;
            alignment = new BeatAlignment(v);

            staff = new Metrics();
            staff.container = new GameObject();

            measure = new Metrics();
        }

        /// <summary>Beat map for the voice.</summary>
        public BeatAlignment alignment { get; }

        /// <summary>The index of the current beat that is active for this measure.</summary>
        public int beatAlignmentIndex { get; set; } = 0;

        /// <summary>The staff object on the current line.</summary>
        public GameObject currentStaff { get; set; }

        /// <summary>Prepares this measure to render the next measure.</summary>
        public void NewMeasure()
        {
            beatAlignmentIndex = 0;
            measure = new Metrics();
            measure.container = new GameObject();
        }

        public void NewStaffline()
        {
            staff = new Metrics();
            staff.container = new GameObject();
        }

        public void UpdateStaffBounding()
        {
            staff.minY = Mathf.Min(staff.minY, measure.minY);
            staff.maxY = Mathf.Max(staff.maxY, measure.maxY);
        }
    }
}
