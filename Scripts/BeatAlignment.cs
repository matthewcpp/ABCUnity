using System;
using System.Collections.Generic;
using UnityEngine;

namespace ABCUnity
{
    public class BeatAlignment
    {
        public class BeatItem
        {
            public List<ABC.Item> items = new List<ABC.Item>();
            public int beatStart;

            public BeatItem(int beatStart)
            {
                this.beatStart = beatStart;
            }
        }

        public class MeasureInfo
        {
            public List<BeatItem> beatItems { get; } = new List<BeatItem>();
            public ABC.Bar bar;
        }

        public List<MeasureInfo> measures { get; private set; }
        TimeSignature timeSignature;

        public void Create(ABC.Voice voice)
        {
            measures = new List<MeasureInfo>();

            if (voice.items.Count == 0) return;

            var timeSignatureItem = voice.items[0] as ABC.TimeSignature;
            if (timeSignatureItem == null)
                throw new BeatAlignmentException("Voice does not initially declare a time signature.");

            timeSignature = TimeSignature.Parse(timeSignatureItem.value);

            float t = 0;
            int currentBeat = 1;

            MeasureInfo measure = new MeasureInfo();
            BeatItem beatItem = new BeatItem(currentBeat);

            for (int i = 1; i < voice.items.Count; i++)
            {
                switch (voice.items[i].type)
                {
                    case ABC.Item.Type.Chord:
                    case ABC.Item.Type.Rest:
                    case ABC.Item.Type.Note:
                        var noteItem = voice.items[i] as ABC.Duration;
                        beatItem.items.Add(noteItem);
                        t += noteItem.duration;

                        if (t >= timeSignature.noteValue) // current beat is filled
                        {
                            measure.beatItems.Add(beatItem);
                            float beatCount = Mathf.Floor(t / timeSignature.noteValue);
                            currentBeat += (int)beatCount;
                            beatItem = new BeatItem(currentBeat);
                            t -= beatCount * timeSignature.noteValue;
                        }

                        break;

                    case ABC.Item.Type.MultiMeasureRest:
                        var measureRest = voice.items[i] as ABC.MultiMeasureRest;

                        if (measureRest.count > 1)
                            throw new LayoutException("Measure Rests of length greater than 1 are not currently supported.");

                        beatItem.items.Add(measureRest);

                        break;

                    case ABC.Item.Type.Bar:
                        if (beatItem.items.Count > 0)
                            measure.beatItems.Add(beatItem);

                        measures.Add(measure);
                        measure = new MeasureInfo();

                        currentBeat = 1;
                        beatItem = new BeatItem(currentBeat);
                        t = 0.0f;
                        break;
                }
            }

            if (beatItem.items.Count > 0)
                measure.beatItems.Add(beatItem);

            if (measure.beatItems.Count > 0)
                measures.Add(measure);
        }
    }
}
