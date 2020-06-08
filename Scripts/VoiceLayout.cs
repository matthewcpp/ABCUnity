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
            public List<Measure> measures = new List<Measure>();
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

            public class Measure
            {
                public Alignment.Measure source { get; }
                public float insertX { get { return bounds.size.x; } }
                public GameObject container;
                public Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
                public List<Element> elements = new List<Element>();
                public List<float> spacers = new List<float>();

                public float minWidth { get { return GetMinWidth(); } }

                public Measure(Alignment.Measure measure)
                {
                    source = measure;
                }

                public void EncapsulateAppendedBounds(Bounds bounding)
                {
                    bounds.Encapsulate(new Bounds(bounding.center + new Vector3(insertX, 0.0f, 0.0f), bounding.size));
                }

                public void AdvaceInsertPos(float amount)
                {
                    var pos = new Vector3(insertX, 0.0f, 0.0f);
                    pos.x += amount;
                    bounds.Encapsulate(pos);
                }

                public Element AddItem(ABC.Item item)
                {
                    var element = new Element(item);
                    elements.Add(element);
                    return element;
                }

                private float GetMinWidth()
                {
                    float minWidth = 0.0f;

                    foreach (var spacer in spacers)
                        minWidth += spacer;

                    foreach (var element in elements)
                        minWidth += element.totalWidth;

                    return minWidth;
                }
            }

            public class Element
            {
                public ABC.Item item { get; }
                public GameObject container { get; set; }
                public NoteInfo info;

                public Element(ABC.Item item)
                {
                    this.item = item;
                }

                public float prefixAmount { get { return info.rootBounding.min.x; } }
                public float totalWidth { get { return info.totalBounding.size.x; } }

                public NoteInfo OffsetNoteInfo(Vector3 offset)
                {
                    return new NoteInfo(
                        new Bounds(info.rootBounding.center + offset, info.rootBounding.size),
                        new Bounds(info.totalBounding.center + offset, info.totalBounding.size));
                }
            }
        }

        public List<ScoreLine> scoreLines { get; } = new List<ScoreLine>();

        public VoiceLayout(ABC.Voice v)
        {
            voice = v;
            alignment = new Alignment();
            alignment.Create(voice);
        }

        public void Init(bool multiline)
        {
            // Partitions measures into their appropriate score lines
            if (multiline)
            {
                foreach (var measure in alignment.measures)
                {
                    if (measure.lineNumber >= scoreLines.Count)
                        scoreLines.Add(new ScoreLine());

                    scoreLines[measure.lineNumber].measures.Add(new ScoreLine.Measure(measure));
                }
            }
            else
            {
                var scoreLine = new ScoreLine();

                foreach (var measure in alignment.measures)
                    scoreLine.measures.Add(new ScoreLine.Measure(measure));

                scoreLines.Add(scoreLine);
            }
        }

        /// <summary>Beat map for the voice.</summary>
        public Alignment alignment { get; }

        /// <summary>The index of the current beat that is active for this measure.</summary>
        public int beatIndex { get; set; } = 0;
    }
}
