using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ABCUnity
{
    class VoiceLayout
    {
        public ABC.Voice voice { get; }

        public class ScoreLine
        {
            public List<Alignment.Measure> measures = new List<Alignment.Measure>();
            public GameObject container;
            public Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

            public Vector3 insertPos
            {
                get { return new Vector3(bounds.size.x, 0.0f, 0.0f); }
            }

            public float insertX { get { return bounds.size.x; } }

            public void EncapsulateAppendedBounds(Bounds bounding)
            {
                bounds.Encapsulate(new Bounds(bounding.center + insertPos, bounding.size));
            }

            public void AdvaceInsertPos(float amount)
            {
                var pos = insertPos;
                pos.x += amount;
                bounds.Encapsulate(pos);
            }
        }

        public List<ScoreLine> scoreLines { get; } = new List<ScoreLine>();

        public VoiceLayout(ABC.Voice v)
        {
            voice = v;
            alignment = new Alignment();
        }

        public void Init()
        {
            alignment.Create(voice);

            // Partitions measures into their appropriate score lines
            foreach (var measure in alignment.measures)
            {
                if (measure.lineNumber >= scoreLines.Count)
                    scoreLines.Add(new ScoreLine());

                scoreLines[measure.lineNumber].measures.Add(measure);
            }
        }

        /// <summary>Beat map for the voice.</summary>
        public Alignment alignment { get; }

        /// <summary>The index of the current beat that is active for this measure.</summary>
        public int beatAlignmentIndex { get; set; } = 0;
    }
}
