using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace ABCUnity
{
    class VoiceLayout
    {
        public ABC.Voice voice { get; }
        public float minY { get; set; } = float.MaxValue;
        public float maxY { get; set; } = float.MinValue;

        public VoiceLayout(ABC.Voice v)
        {
            voice = v;
            alignment = new BeatAlignment(v);
            container = new GameObject();
        }

        public void AdjustMinMax(Bounds b)
        {
            if (b.min.y < minY)
                minY = b.min.y;

            if (b.max.y > maxY)
                maxY = b.max.y;
        }

        public float height { get { return maxY - minY; } }

        public BeatAlignment alignment { get; }
        public int beatAlignmentIndex { get; set; } = 0;

        public GameObject currentStaff { get; set; }
        public GameObject container { get; set; }

        public Vector3 insertPos = Vector3.zero;
    }

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
        Vector3 insertPos = Vector3.zero;

        List<VoiceLayout> layouts = new List<VoiceLayout>();

        void LayoutStaff(VoiceLayout layout)
        {
            var staff = cache.GetSpriteObject("Staff");
            layout.currentStaff = staff.gameObject;
            layout.currentStaff.transform.parent = layout.container.transform;
            layout.currentStaff.transform.localPosition = layout.insertPos;

            layout.AdjustMinMax(staff.bounds);
            layout.insertPos.x += staffPadding;

            var clef = cache.GetSpriteObject($"Clef_{layout.voice.clef.ToString()}");
            clef.transform.parent = layout.container.transform;
            clef.transform.localPosition = layout.insertPos;

            layout.AdjustMinMax(clef.bounds);
            layout.insertPos.x += clefAdvance;
        }

        void LayoutTune()
        {
            if (tune == null) return;

            staffOffset = new Vector2(-bounding.bounds.extents.x, bounding.bounds.extents.y);

            Vector3 scale = this.gameObject.transform.localScale;
            this.gameObject.transform.localScale = Vector3.one;

            // create the layout structures for each voice
            for (int i =0 ; i < tune.voices.Count; i++)
            {
                var layout = new VoiceLayout(tune.voices[i]);
                LayoutStaff(layout);

                layouts.Add(layout);
            }

            int beatCount = 4;

            for (int measure = 0; measure < layouts[0].alignment.measures.Count; measure++)
            {
                foreach (var layout in layouts)
                    layout.beatAlignmentIndex = 0;

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

                        maxBeatX = Math.Max(maxBeatX, layout.insertPos.x);
                    }

                    // in order to preserve alignment, all layouts will advance to the furthest position of the current beat marker
                    foreach (var layout in layouts)
                        layout.insertPos.x = maxBeatX;
                }

                // render the bar to end the measure
                foreach (var layout in layouts)
                {
                    var measureInfo = layout.alignment.measures[measure];
                    LayoutBar(measureInfo.bar, layout);

                }
            }

            // final sizing and positioning of staff
            foreach (var layout in layouts)
            {
                AdjustStaffScale(layout);

                layout.container.transform.parent = this.transform;
                layout.container.transform.localPosition = new Vector3(staffOffset.x, staffOffset.y - (layout.maxY * layoutScale), 0.0f);
                layout.container.transform.localScale = new Vector3(layoutScale, layoutScale, layoutScale);

                staffOffset.y -= (layout.height + staffMargin) * layoutScale;
            }

            this.gameObject.transform.localScale = scale;
        }

        void AdjustStaffScale(VoiceLayout layout)
        {
            var currentWidth = layout.currentStaff.GetComponent<SpriteRenderer>().bounds.size.x;
            var scaleX = layout.insertPos.x / currentWidth;
            layout.currentStaff.transform.localScale = new Vector3(scaleX, 1.0f, 1.0f);
        }

        void LayoutBar(ABC.BarItem barItem, VoiceLayout layout)
        {
            var barObj = cache.GetSpriteObject("Bar_Line");
            barObj.transform.parent = layout.container.transform;
            barObj.transform.localPosition = layout.insertPos;

            layout.insertPos.x += noteAdvance / 2.0f;
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
            note.transform.parent = layout.container.transform;
            note.transform.localPosition = layout.insertPos + new Vector3(0.0f, noteStep * stepCount, 0.0f);

            layout.AdjustMinMax(note.bounds);
            layout.insertPos.x += noteAdvance;
        }
        
        void InsertStaffMark(int stepCount, VoiceLayout layout)
        {
            var mark = cache.GetSpriteObject("Staff_Mark");
            mark.transform.parent = layout.container.transform;
            mark.transform.localPosition = layout.insertPos + new Vector3(0.0f, noteStep * stepCount, 0.0f);
        }
    }
}

