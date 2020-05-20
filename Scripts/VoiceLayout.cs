using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ABCUnity
{
    class VoiceLayout
    {
        public class Metrics
        {
            Bounds boundingBox;

            public Bounds bounds => boundingBox;

            public Metrics()
            {
                boundingBox.SetMinMax(Vector3.one, Vector3.zero);
            }

            public void UpdateBounds(Bounds b)
            {
                if (boundingBox.max.x < boundingBox.min.x)
                    boundingBox = b;
                else
                {
                    boundingBox.Encapsulate(b);
                }
            }

            public Vector3 position = Vector3.zero;

            public GameObject container;
        }

        public ABC.Voice voice { get; }

        public Metrics staff { get; private set; }
        public Metrics measure { get; private set; }

        public List<List<BeatAlignment.Measure>> scoreLines { get; } = new List<List<BeatAlignment.Measure>>();

        public VoiceLayout(ABC.Voice v)
        {
            voice = v;
            alignment = new BeatAlignment();

            staff = new Metrics();
            staff.container = new GameObject();
            measureVertices = new List<Vector3>();

            measure = new Metrics();
        }

        public void Align()
        {
            alignment.Create(voice);

            // Partitions measures into their appropriate score lines
            foreach (var measure in alignment.measures)
            {
                if (measure.lineNumber >= scoreLines.Count)
                    scoreLines.Add(new List<BeatAlignment.Measure>());

                scoreLines[measure.lineNumber].Add(measure);
            }
        }

        /// <summary>Beat map for the voice.</summary>
        public BeatAlignment alignment { get; }

        /// <summary>The index of the current beat that is active for this measure.</summary>
        public int beatAlignmentIndex { get; set; } = 0;

        /// <summary>The staff object on the current line.</summary>
        public GameObject currentStaff { get; set; }

        public List<Vector3> measureVertices { get; private set; }

        /// <summary>Prepares this measure to render the next measure.</summary>
        public void NewMeasure()
        {
            beatAlignmentIndex = 0;
            measure = new Metrics();
            measure.container = new GameObject();
            measureVertices = new List<Vector3>();
        }

        public void NewStaffline()
        {
            staff = new Metrics();
            staff.container = new GameObject();
        }
        public void UpdateStaffBounding()
        {
            staff.UpdateBounds(measure.bounds);
        }
    }
}
