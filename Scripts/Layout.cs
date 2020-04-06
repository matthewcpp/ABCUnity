using System;
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
        }

        public void AdjustMinMax(Bounds b)
        {
            if (b.min.y < minY)
                minY = b.min.y;

            if (b.max.y > maxY)
                maxY = b.max.y;
        }

        public float height { get { return maxY - minY; } }
    }

    public class Layout : MonoBehaviour
    {
        [SerializeField]
        SpriteAtlas spriteAtlas; // set in editor

        public string AbcCode;

        private SpriteCache cache;

        private ABC.Tune tune;
        private BoxCollider2D bounding;

        public void Start()
        {
            bounding = this.GetComponent<BoxCollider2D>();
            cache = new SpriteCache(spriteAtlas);

            if (AbcCode.Length > 0)
                LoadTune(AbcCode);
        }

        public void LoadTune(string abc)
        {
            try
            {
                tune = ABC.Tune.Load(AbcCode);
                Create();
            }
            catch (ABC.ParseException e)
            {
                Debug.Log(e.Message);
            }
        }

        Dictionary<ABC.Clef, ABC.Note.Value> cleffZero = new Dictionary<ABC.Clef, ABC.Note.Value>()
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

        List<VoiceLayout> voiceLayouts = new List<VoiceLayout>();
        VoiceLayout layout;

        GameObject currentStaff;
        GameObject container;

        void Create()
        {
            staffOffset = new Vector2(-bounding.bounds.extents.x, bounding.bounds.extents.y);

            Vector3 scale = this.gameObject.transform.localScale;
            this.gameObject.transform.localScale = Vector3.one;

            foreach (var voice in tune.voices)
            {
                container = new GameObject();
                layout = new VoiceLayout(voice);

                insertPos = Vector3.zero;

                LayoutStaff();

                foreach (var item in voice.items)
                {
                    switch (item.type)
                    {
                        case ABC.Item.Type.Note:
                            LayoutNote(item as ABC.NoteItem);
                            break;

                        case ABC.Item.Type.Bar:
                            LayoutBar(item as ABC.BarItem);
                            break;
                    };
                }

                AdjustStaffScale();

                container.transform.parent = this.transform;
                container.transform.localPosition = new Vector3(staffOffset.x, staffOffset.y - layout.maxY, 0.0f);

                staffOffset.y -= (layout.height + staffMargin);
            }

            this.gameObject.transform.localScale = scale;
        }

        void AdjustStaffScale()
        {
            var currentWidth = currentStaff.GetComponent<SpriteRenderer>().bounds.size.x;
            var scaleX = (insertPos.x + noteAdvance) / currentWidth;
            currentStaff.transform.localScale = new Vector3(scaleX, 1.0f, 1.0f);
        }
            
        void LayoutBar(ABC.BarItem barItem)
        {
            var barObj = cache.GetSpriteObject("Bar_Line");
            barObj.transform.parent = container.transform;
            barObj.transform.localPosition = insertPos;

            insertPos.x += noteAdvance / 2.0f;
        }

        void LayoutStaff()
        {
            var staff = cache.GetSpriteObject("Staff");
            currentStaff = staff.gameObject;
            currentStaff.transform.parent = container.transform;
            currentStaff.transform.localPosition = insertPos;

            layout.AdjustMinMax(staff.bounds);
            insertPos.x += staffPadding;

            var clef = cache.GetSpriteObject($"Clef_{layout.voice.clef.ToString()}");
            clef.transform.parent = container.transform;
            clef.transform.localPosition = insertPos;

            layout.AdjustMinMax(clef.bounds);
            insertPos.x += clefAdvance;
        }

        enum NoteDirection
        {
            Down, Up
        }

        enum StaffMarker
        {
            None, Middle, Above, Below
        }

        void LayoutNote(ABC.NoteItem noteItem)
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
                    InsertStaffMark(sc);
            }

            else if (stepCount > 8) // above the staff
            {
                staffMarker = stepCount % 2 == 0 ? StaffMarker.Below : StaffMarker.Middle;

                for (int sc = stepCount - 2; sc > 8; sc -= 2)
                    InsertStaffMark(sc);
            }

            var note = cache.GetSpriteObject($"Note_{noteName}_{noteDirection.ToString()}_{staffMarker.ToString()}");
            note.transform.parent = container.transform;
            note.transform.localPosition = insertPos + new Vector3(0.0f, noteStep * stepCount, 0.0f);

            layout.AdjustMinMax(note.bounds);
            insertPos.x += noteAdvance;
        }

        void InsertStaffMark(int stepCount)
        {
            var mark = cache.GetSpriteObject("Staff_Mark");
            mark.transform.parent = container.transform;
            mark.transform.localPosition = insertPos + new Vector3(0.0f, noteStep * stepCount, 0.0f);
        }
    }
}

