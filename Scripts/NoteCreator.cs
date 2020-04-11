using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace ABCUnity
{
    class NoteCreator
    {
        SpriteCache spriteCache;

        public NoteCreator(SpriteCache spriteCache)
        {
            this.spriteCache = spriteCache;
        }

        static Dictionary<ABC.Clef, ABC.Note.Value> cleffZero = new Dictionary<ABC.Clef, ABC.Note.Value>()
        {
            { ABC.Clef.Treble, ABC.Note.Value.F4}, { ABC.Clef.Bass, ABC.Note.Value.A2}
        };

        enum NoteDirection
        {
            Down, Up
        }

        enum StaffMarker
        {
            None, Middle, Above, Below
        }

        const float noteStep = 0.28f;

        public SpriteRenderer CreateNote(ABC.Note note, ABC.Clef clef, GameObject container, Vector3 offset)
        {
            int stepCount = note.value - cleffZero[clef];

            var noteName = note.length.ToString();
            var noteDirection = NoteDirection.Up;
            var staffMarker = StaffMarker.None;

            if (stepCount > 3)
                noteDirection = NoteDirection.Down;

            if (stepCount < -2)  // below the staff
            {
                staffMarker = stepCount % 2 == 0 ? StaffMarker.Above : StaffMarker.Middle;

                for (int sc = stepCount + 2; sc < -2; sc += 2)
                    CreateStaffMark(sc, container, offset);
            }

            else if (stepCount > 8) // above the staff
            {
                staffMarker = stepCount % 2 == 0 ? StaffMarker.Below : StaffMarker.Middle;

                for (int sc = stepCount - 2; sc > 8; sc -= 2)
                    CreateStaffMark(sc, container, offset);
            }

            var noteObj = spriteCache.GetSpriteObject($"Note_{noteName}_{noteDirection.ToString()}_{staffMarker.ToString()}");
            noteObj.transform.parent = container.transform;
            noteObj.transform.localPosition = offset + new Vector3(0.0f, noteStep * stepCount, 0.0f);

            return noteObj;
        }

        public List<SpriteRenderer> CreateChord(ABC.Note[] notes, ABC.Clef clef, GameObject container, Vector3 offset)
        {
            var items = new List<SpriteRenderer>();

            foreach(var note in notes)
            {
                items.Add(CreateNote(note, clef, container, offset));
            }
            
            return items;
        }

        void CreateStaffMark(int stepCount, GameObject container, Vector3 offset)
        {
            var mark = spriteCache.GetSpriteObject("Staff_Mark");
            mark.transform.parent = container.transform;
            mark.transform.localPosition = offset + new Vector3(0.0f, noteStep * stepCount, 0.0f);
        }
    }
}