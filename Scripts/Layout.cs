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

        public ABC.Tune tune { get; private set; }
        private BoxCollider2D bounding;

        public void Awake()
        {
            bounding = this.GetComponent<BoxCollider2D>();
            cache = new SpriteCache(spriteAtlas);
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

        static Dictionary<ABC.Clef, ABC.Note.Value> cleffZero = new Dictionary<ABC.Clef, ABC.Note.Value>()
        {
            { ABC.Clef.Treble, ABC.Note.Value.F4}, { ABC.Clef.Bass, ABC.Note.Value.A2}
        };

        const float noteStep = 0.28f;
        const float staffPadding = 0.3f;
        const float staffMargin = 0.2f;
        const float clefAdvance = 2.0f;
        const float noteAdvance = 1.5f;

        Vector2 staffOffset;

        List<VoiceLayout> layouts = new List<VoiceLayout>();

        private float horizontalMax;

        void LayoutTune()
        {
            if (tune == null) return;
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

            int beatCount = 4; // TODO: parse the time signature.

            for (int measure = 0; measure < layouts[0].alignment.measures.Count; measure++)
            {
                foreach (var layout in layouts) // layout.NewMeasure()
                    layout.NewMeasure();

                for (int beat = 1; beat <= beatCount; beat++)
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
                                    case ABC.NoteItem.Type.Note:
                                        LayoutNote(item as ABC.NoteItem, layout);
                                        break;
                                }
                            }

                            if (layout.beatAlignmentIndex < measureInfo.beatItems.Count - 1)
                                layout.beatAlignmentIndex += 1;
                        }

                        maxBeatX = Math.Max(maxBeatX, layout.measurePos.x);
                    }

                    // in order to preserve alignment, all layouts will advance to the furthest position of the current beat marker
                    foreach (var layout in layouts)
                        layout.measurePos.x = maxBeatX;
                }

                bool newLineNeeded = false;

                // render the bar to end the measure and ensure they will all fit on new staff line
                foreach (var layout in layouts)
                {
                    var measureInfo = layout.alignment.measures[measure];
                    LayoutBar(measureInfo.bar, layout);

                    if (layout.staffPos.x + layout.measurePos.x > horizontalMax)
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
                    layout.measureContainer.transform.localPosition = layout.staffPos;
                    layout.measureContainer.transform.parent = layout.stafflineContainer.transform;
                    layout.staffPos.x += layout.measurePos.x;
                    layout.UpdateStaffBounding();
                }
            }

            FinalizeStaffLines();

            this.gameObject.transform.localScale = scale;
        }

        /// <summary>Calculates the final size of the staff and positions it correctly relative to the container.</summary>
        void FinalizeStaffLines()
        {
            foreach (var layout in layouts)
            {
                AdjustStaffScale(layout);

                layout.stafflineContainer.transform.parent = this.transform;
                layout.stafflineContainer.transform.localPosition = new Vector3(staffOffset.x, staffOffset.y - (layout.staffMaxY * layoutScale), 0.0f);
                layout.stafflineContainer.transform.localScale = new Vector3(layoutScale, layoutScale, layoutScale);

                staffOffset.y -= (layout.height + staffMargin) * layoutScale;
            }
        }

        void LayoutStaff(VoiceLayout layout)
        {
            var staff = cache.GetSpriteObject("Staff");
            layout.currentStaff = staff.gameObject;
            layout.currentStaff.transform.parent = layout.stafflineContainer.transform;
            layout.currentStaff.transform.localPosition = Vector3.zero; // validate me

            layout.UpdateStaffBounds(staff.bounds);
            layout.staffPos.x += staffPadding;

            var clef = cache.GetSpriteObject($"Clef_{layout.voice.clef.ToString()}");
            clef.transform.parent = layout.stafflineContainer.transform;
            clef.transform.localPosition = layout.staffPos;

            layout.UpdateStaffBounds(clef.bounds);
            layout.staffPos.x += clefAdvance;
        }

        void AdjustStaffScale(VoiceLayout layout)
        {
            var currentWidth = layout.currentStaff.GetComponent<SpriteRenderer>().bounds.size.x;
            var scaleX = layout.staffPos.x / currentWidth;
            layout.currentStaff.transform.localScale = new Vector3(scaleX, 1.0f, 1.0f);
        }

        void LayoutBar(ABC.BarItem barItem, VoiceLayout layout)
        {
            var barObj = cache.GetSpriteObject("Bar_Line");
            barObj.transform.parent = layout.measureContainer.transform;
            barObj.transform.localPosition = layout.measurePos;

            layout.measurePos.x += noteAdvance / 2.0f;
        }

        enum NoteDirection
        {
            Down, Up
        }

        enum StaffMarker
        {
            None, Middle, Above, Below
        }
        
        void LayoutNote(ABC.NoteItem noteItem, VoiceLayout layout)
        {
            int stepCount = noteItem.note.value - cleffZero[layout.voice.clef];

            var noteName = noteItem.note.length.ToString();
            var noteDirection = NoteDirection.Up;
            var staffMarker = StaffMarker.None;

            if (stepCount > 3)
                noteDirection = NoteDirection.Down;

            if (stepCount < -2)  // below the staff
            {
                staffMarker = stepCount % 2 == 0 ? StaffMarker.Above : StaffMarker.Middle;

                for (int sc = stepCount + 2; sc < -2; sc += 2)
                    InsertStaffMark(sc, layout);
            }

            else if (stepCount > 8) // above the staff
            {
                staffMarker = stepCount % 2 == 0 ? StaffMarker.Below : StaffMarker.Middle;

                for (int sc = stepCount - 2; sc > 8; sc -= 2)
                    InsertStaffMark(sc, layout);
            }

            var note = cache.GetSpriteObject($"Note_{noteName}_{noteDirection.ToString()}_{staffMarker.ToString()}");
            note.transform.parent = layout.measureContainer.transform;
            note.transform.localPosition = layout.measurePos + new Vector3(0.0f, noteStep * stepCount, 0.0f);

            layout.UpdateMeasureBounds(note.bounds);
            layout.measurePos.x += noteAdvance;
        }
        
        void InsertStaffMark(int stepCount, VoiceLayout layout)
        {
            var mark = cache.GetSpriteObject("Staff_Mark");
            mark.transform.parent = layout.measureContainer.transform;
            mark.transform.localPosition = layout.measurePos + new Vector3(0.0f, noteStep * stepCount, 0.0f);
        }
    }
}

