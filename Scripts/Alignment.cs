﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace ABCUnity
{
    public class Alignment
    {
        public class Item
        {
            public ABC.Item item { get; }
            public GameObject container;
            public NoteInfo info;

            public Item(ABC.Item item)
            {
                this.item = item;
            }
        }
        
        public class Beat
        {
            public List<Item> items = new List<Item>();
            public int beatStart { get; }

            public Beat(int beatStart)
            {
                this.beatStart = beatStart;
            }

            public Vector2 minSize = Vector2.zero;
        }

        public class Measure
        {
            public List<Beat> beats { get; } = new List<Beat>();
            public Item bar;
            public int lineNumber { get; set; }

            public Measure(int lineNumber)
            {
                this.lineNumber = lineNumber;
            }

            public Vector2 minSize = Vector2.zero;
        }

        public List<Measure> measures { get; private set; }
        TimeSignature timeSignature;

        public void Create(ABC.Voice voice)
        {
            measures = new List<Measure>();

            if (voice.items.Count == 0) return;

            var timeSignatureItem = voice.items[0] as ABC.TimeSignature;
            if (timeSignatureItem == null)
                throw new BeatAlignmentException("Voice does not initially declare a time signature.");

            timeSignature = TimeSignature.Parse(timeSignatureItem.value);

            float t = 0;
            int currentBeat = 1;
            int lineNumber = 0;

            Measure measure = new Measure(lineNumber);
            Beat beat = new Beat(currentBeat);

            for (int i = 1; i < voice.items.Count; i++)
            {
                switch (voice.items[i].type)
                {
                    case ABC.Item.Type.Chord:
                    case ABC.Item.Type.Rest:
                    case ABC.Item.Type.Note:
                        var noteItem = voice.items[i] as ABC.Duration;
                        beat.items.Add(new Item(noteItem));
                        t += noteItem.duration;

                        if (t >= timeSignature.noteValue) // current beat is filled
                        {
                            measure.beats.Add(beat);
                            float beatCount = Mathf.Floor(t / timeSignature.noteValue);
                            currentBeat += (int)beatCount;
                            beat = new Beat(currentBeat);
                            t -= beatCount * timeSignature.noteValue;
                        }

                        break;

                    case ABC.Item.Type.MultiMeasureRest:
                        var measureRest = voice.items[i] as ABC.MultiMeasureRest;

                        if (measureRest.count > 1)
                            throw new LayoutException("Measure Rests of length greater than 1 are not currently supported.");

                        beat.items.Add(new Item(measureRest));

                        break;

                    case ABC.Item.Type.Bar:
                        measure.bar = new Item(voice.items[i]);

                        if (beat.items.Count > 0)
                            measure.beats.Add(beat);

                        measures.Add(measure);
                        measure = new Measure(lineNumber);

                        currentBeat = 1;
                        beat = new Beat(currentBeat);
                        t = 0.0f;
                        break;

                    case ABC.Item.Type.LineBreak:
                        lineNumber += 1;
                        measure.lineNumber = lineNumber;
                        break;
                }
            }

            if (beat.items.Count > 0)
                measure.beats.Add(beat);

            if (measure.beats.Count > 0)
                measures.Add(measure);
        }
    }
}
