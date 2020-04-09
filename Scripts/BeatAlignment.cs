using System;
using System.Collections.Generic;

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
            public ABC.BarItem bar;
        }

        public BeatAlignment(ABC.Voice voice)
        {
            Create(voice);
        }

        public List<MeasureInfo> measures { get; private set; }

        //private int beatCount;
        private float beatValue;

        // Temp implementation
        private void ParseTimeSignature(ABC.TimeSignatureItem timeSignature)
        {
            //beatCount = 4;
            beatValue = 0.25f;
        }

        public void Create(ABC.Voice voice)
        {
            measures = new List<MeasureInfo>();

            if (voice.items.Count == 0) return;

            if (voice.items[0].type != ABC.Item.Type.TimeSignature)
                throw new BeatAlignmentException("Voice does not initially declare a time signature.");

            ParseTimeSignature(voice.items[0] as ABC.TimeSignatureItem);

            float t = 0;
            int currentBeat = 1;

            MeasureInfo measure = new MeasureInfo();
            BeatItem beatItem = new BeatItem(currentBeat);

            for (int i = 1; i < voice.items.Count; i++)
            {
                switch (voice.items[i].type)
                {
                    case ABC.Item.Type.Note:
                        beatItem.items.Add(voice.items[i]);
                        var noteItem = voice.items[i] as ABC.NoteItem;
                        float noteTime = 1.0f / (float)noteItem.note.length;
                        t += noteTime;

                        if (t >= beatValue) // current beat is filled
                        {
                            measure.beatItems.Add(beatItem);
                            currentBeat += (int)Math.Round(noteTime / beatValue);
                            beatItem = new BeatItem(currentBeat);
                            t = 0.0f;
                        }
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
