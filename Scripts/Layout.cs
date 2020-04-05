using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace ABCUnity
{
    public class Layout : MonoBehaviour
    {
        [SerializeField]
        SpriteAtlas spriteAtlas; // set in editor

        public string AbcCode;

        private SpriteCache cache;

        private ABC.Tune tune;

        public void Start()
        {
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

        const float staffHeight = 2.25f;
        const float staffClear = 2.0f;
        const float noteStep = 0.28f;
        const float staffPadding = 0.3f;
        const float clefAdvance = 2.0f;
        const float noteAdvance = 1.5f;

        Vector3 insertPos = Vector3.zero;

        GameObject currentStaff;

        void Create()
        {
            Vector3 scale = this.gameObject.transform.localScale;
            this.gameObject.transform.localScale = Vector3.one;

            foreach (var voice in tune.voices)
            {
                LayoutStaff(voice);

                foreach (var item in voice.items)
                {
                    switch (item.type)
                    {
                        case ABC.Item.Type.Note:
                            LayoutNote(item as ABC.NoteItem, voice);
                            break;

                        case ABC.Item.Type.Bar:
                            LayoutBar(item as ABC.BarItem);
                            break;
                    };
                }

                AdjustStaffScale();

                insertPos.x = 0.0f;
                insertPos.y -= staffClear;
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
            var barObj = cache.GetObject("bar_line");
            barObj.transform.parent = this.transform;
            barObj.transform.localPosition = insertPos;

            insertPos.x += noteAdvance / 2.0f;
        }

        void LayoutStaff(ABC.Voice voice)
        {
            insertPos.y -= staffHeight;
            currentStaff = cache.GetObject("staff");
            currentStaff.transform.parent = this.transform;
            currentStaff.transform.localPosition = insertPos;

            insertPos.x += staffPadding;

            var clef = cache.GetObject(voice.clef == ABC.Clef.Treble ? "clef_treble" : "clef_bass");
            clef.transform.parent = this.transform;
            clef.transform.localPosition = insertPos;

            insertPos.x += clefAdvance;
        }

        void LayoutNote(ABC.NoteItem noteItem, ABC.Voice voice)
        {
            int stepCount = noteItem.note.value - cleffZero[voice.clef];

            string note_handle = "quarternote";

            if (stepCount > 3)
                note_handle += "_down";
            else
                note_handle += "_up";

            if (stepCount < -1 && stepCount % 2 != 0)
                note_handle += "_striked";

            var obj = cache.GetObject(note_handle);
            obj.transform.parent = this.transform;

            obj.transform.localPosition = insertPos + new Vector3(0.0f, noteStep * stepCount, 0.0f);
            insertPos.x += noteAdvance;
        }
    }
}

