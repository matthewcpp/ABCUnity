using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace ABCUnity
{
    public class Layout : MonoBehaviour
    {
        [SerializeField]
        SpriteAtlas spriteAtlas; // set in editor

        [SerializeField]
        float layoutScale = 0.5f;

        private SpriteCache cache;
        private NoteCreator notes;

        public ABC.Tune tune { get; private set; }
        private BoxCollider2D bounding;

        public void Awake()
        {
            bounding = this.GetComponent<BoxCollider2D>();
            cache = new SpriteCache(spriteAtlas);
            notes = new NoteCreator(cache);
        }

        public void LoadString(string abc)
        {
            try
            {
                tune = ABC.Tune.Load(abc);
                LayoutTune();
            }
            catch (ABC.ParseException e)
            {
                Debug.Log(e.Message);
            }
        }

        public void LoadStream(Stream stream)
        {
            try
            {
                tune = ABC.Tune.Load(stream);
                LayoutTune();
            }
            catch (ABC.ParseException e)
            {
                Debug.Log(e.Message);
            }
        }
        
        const float staffPadding = 0.3f;
        const float staffMargin = 0.2f;
        const float clefAdvance = 2.0f;
        const float noteAdvance = 0.75f;

        Vector2 staffOffset;

        List<VoiceLayout> layouts = new List<VoiceLayout>();

        private float horizontalMax;

        void LayoutTune()
        {
            if (tune == null) return;
            var timeSignature = GetTimeSignature();

            horizontalMax = bounding.size.x * 1.0f / layoutScale;

            staffOffset = new Vector2(-bounding.bounds.extents.x, bounding.bounds.extents.y);

            Vector3 scale = this.gameObject.transform.localScale;
            this.gameObject.transform.localScale = Vector3.one;

            // create the layout structures for each voice
            for (int i =0 ; i < tune.voices.Count; i++)
            {
                var layout = new VoiceLayout(tune.voices[i]);
                layouts.Add(layout);
                LayoutStaff(layout);
            }

            for (int measure = 0; measure < layouts[0].alignment.measures.Count; measure++)
            {
                foreach (var layout in layouts)
                    layout.NewMeasure();

                for (int beat = 1; beat <= timeSignature.beatCount; beat++)
                {
                    float maxBeatX = float.MinValue;

                    foreach (var layout in layouts)
                    {
                        var measureInfo = layout.alignment.measures[measure];
                        var beatInfo = measureInfo.beatItems[layout.beatAlignmentIndex];

                        // if this beat is the start of a new group of notes render them
                        if (beatInfo.beatStart == beat)
                        {
                            foreach (var item in beatInfo.items)
                            {
                                switch (item.type)
                                {
                                    case ABC.Item.Type.Note:
                                        LayoutNote(item as ABC.NoteItem, layout);
                                        break;

                                    case ABC.Item.Type.Chord:
                                        LayoutChord(item as ABC.ChordItem, layout);
                                        break;
                                }
                            }

                            if (layout.beatAlignmentIndex < measureInfo.beatItems.Count - 1)
                                layout.beatAlignmentIndex += 1;
                        }

                        maxBeatX = Math.Max(maxBeatX, layout.measure.position.x);
                    }

                    // in order to preserve alignment, all layouts will advance to the furthest position of the current beat marker
                    foreach (var layout in layouts)
                        layout.measure.position.x = maxBeatX;
                }

                bool newLineNeeded = false;

                // render the bar to end the measure and ensure they will all fit on new staff line
                foreach (var layout in layouts)
                {
                    var measureInfo = layout.alignment.measures[measure];
                    LayoutBar(measureInfo.bar, layout);

                    if (layout.staff.position.x + layout.measure.position.x > horizontalMax)
                        newLineNeeded = true;
                }

                if (newLineNeeded)
                {
                    FinalizeStaffLines();

                    foreach (var layout in layouts)
                    {
                        layout.NewStaffline();
                        LayoutStaff(layout);
                    }
                }

                // Add the measure to the staff line
                foreach (var layout in layouts)
                {
                    layout.measure.container.transform.localPosition = layout.staff.position;
                    layout.measure.container.transform.parent = layout.staff.container.transform;
                    layout.staff.position.x += layout.measure.position.x;
                    layout.UpdateStaffBounding();
                }
            }

            FinalizeStaffLines();

            this.gameObject.transform.localScale = scale;
        }

        TimeSignature GetTimeSignature()
        {
            TimeSignature result = null;

            for (int i = 0; i < tune.voices.Count; i++)
            {
                var timeSignatureItem = tune.voices[i].items[0] as ABC.TimeSignatureItem;
                if (timeSignatureItem == null)
                    throw new BeatAlignmentException($"Voice {i} does not initially declare a time signature.");

                var timeSignature = TimeSignature.Parse(timeSignatureItem.timeSignature);

                if (result == null)
                    result = timeSignature;
                else if (!timeSignature.Equals(result))
                    throw new LayoutException("All voices should have the same time signature");

            }

            return result;
        }

        /// <summary>Calculates the final size of the staff and positions it correctly relative to the container.</summary>
        void FinalizeStaffLines()
        {
            foreach (var layout in layouts)
            {
                AdjustStaffScale(layout);

                layout.staff.container.transform.parent = this.transform;
                layout.staff.container.transform.localPosition = new Vector3(staffOffset.x, staffOffset.y - (layout.staff.maxY * layoutScale), 0.0f);
                layout.staff.container.transform.localScale = new Vector3(layoutScale, layoutScale, layoutScale);

                staffOffset.y -= (layout.staff.height + staffMargin) * layoutScale;
            }
        }

        void LayoutStaff(VoiceLayout layout)
        {
            var staff = cache.GetSpriteObject("Staff");
            layout.currentStaff = staff.gameObject;
            layout.currentStaff.transform.parent = layout.staff.container.transform;
            layout.currentStaff.transform.localPosition = layout.staff.position;

            layout.staff.UpdateBounds(staff.bounds);
            layout.staff.position.x += staffPadding;

            var clef = cache.GetSpriteObject($"Clef_{layout.voice.clef.ToString()}");
            clef.transform.parent = layout.staff.container.transform;
            clef.transform.localPosition = layout.staff.position;

            layout.staff.UpdateBounds(clef.bounds);
            layout.staff.position.x += clefAdvance;
        }

        void AdjustStaffScale(VoiceLayout layout)
        {
            var currentWidth = layout.currentStaff.GetComponent<SpriteRenderer>().bounds.size.x;
            var scaleX = layout.staff.position.x / currentWidth;
            layout.currentStaff.transform.localScale = new Vector3(scaleX, 1.0f, 1.0f);
        }

        void LayoutBar(ABC.BarItem barItem, VoiceLayout layout)
        {
            var barObj = cache.GetSpriteObject("Bar_Line");
            barObj.transform.parent = layout.measure.container.transform;
            barObj.transform.localPosition = layout.measure.position;

            layout.measure.position.x += noteAdvance / 2.0f;
        }

        void LayoutChord(ABC.ChordItem chordItem, VoiceLayout layout)
        {
            var chord = notes.CreateChord(chordItem.notes, layout.voice.clef, layout.measure.container, layout.measure.position);

            Bounds chordBounds = chord[0].bounds;

            for (int i = 1; i < chord.Count; i++)
            {
                layout.measure.UpdateBounds(chord[i].bounds);
                chordBounds.Encapsulate(chord[i].bounds);
            }

            layout.measure.position.x = chordBounds.max.x + noteAdvance;
        }
        
        void LayoutNote(ABC.NoteItem noteItem, VoiceLayout layout)
        {
            var note = notes.CreateNote(noteItem.note, layout.voice.clef, layout.measure.container, layout.measure.position);
            layout.measure.UpdateBounds(note.bounds);
            layout.measure.position.x = note.bounds.max.x + noteAdvance;
        }
    }
}

