﻿using System;
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

        static Dictionary<ABC.Clef, ABC.Note.Value> clefZero = new Dictionary<ABC.Clef, ABC.Note.Value>()
        {
            { ABC.Clef.Treble, ABC.Note.Value.F4}, { ABC.Clef.Bass, ABC.Note.Value.A2}
        };

        public enum NoteDirection
        {
            Default, Down, Up
        }

        enum StaffMarker
        {
            None, Middle, Above, Below
        }

        const float noteStep = 0.28f;

        void ResolveStaffMarkers(int stepCount, GameObject container, Vector3 offset)
        {
            if (stepCount < -2)  // below the staff
            {
                int stepOffset = stepCount % 2 == 0 ? 1 : 0;
                for (int sc = stepCount + 2 + stepOffset; sc < -2; sc += 2)
                    CreateStaffMark(sc, container, offset);

                CreateStaffMark(stepCount + stepOffset, container, offset);
            }

            else if (stepCount > 8) // above the staff
            {
                for (int sc = stepCount - 2; sc > 8; sc -= 2)
                    CreateStaffMark(sc, container, offset);
                
                CreateStaffMark(stepCount % 2 == 0 ? stepCount - 1 : stepCount, container, offset);
            }
        }

        public SpriteRenderer CreateNote(ABC.Note note, ABC.Clef clef, GameObject container, Vector3 offset, NoteDirection directionOverride = NoteDirection.Default)
        {
            int stepCount = note.value - clefZero[clef];

            var noteName = note.length.ToString();
            var noteDirection = directionOverride;
            ResolveStaffMarkers(stepCount, container, offset);

            if (noteDirection == NoteDirection.Default)
                noteDirection = stepCount > 3 ? NoteDirection.Down : NoteDirection.Up;

            var noteObj = spriteCache.GetSpriteObject($"Note_{noteName}_{noteDirection.ToString()}_None");
            noteObj.transform.parent = container.transform;
            noteObj.transform.localPosition = offset + new Vector3(0.0f, noteStep * stepCount, 0.0f);

            return noteObj;
        }

        const float chordDotOffset = 0.67f;
        const float notePadding = 0.1f;

        SpriteRenderer AddChordDot(ABC.Note note, ABC.Clef clef, NoteDirection noteDirection, GameObject container, Vector3 offset)
        {
            int stepCount = note.value - clefZero[clef];
            ResolveStaffMarkers(stepCount, container, offset);

            var notePos = new Vector3(noteDirection == NoteDirection.Up ? chordDotOffset : -chordDotOffset, noteStep * stepCount, 0.0f);

            var noteName = note.length.ToString();
            var dot = spriteCache.GetSpriteObject($"Chord_{noteName}");
            dot.transform.parent = container.transform;
            dot.transform.localPosition = offset + notePos;

            return dot;
        }

        /// <summary>
        /// The direction of the chord stem is chosen to minimize stem length outside the staff.
        /// The direction is chosen by examining the extreme notes of the chord and picking the direction based on the note which is farthest away from the value of the middle staffline.
        /// Precondition: notes array should be sorted in ascending order.
        /// </summary>
        NoteDirection DetermineChordNoteDirection(ABC.Note[] sortedNotes, ABC.Clef clef)
        {
            var direction = NoteDirection.Down;

            var clefCenter = clefZero[clef] + 3;
            int lowDistance = Math.Abs(sortedNotes[0].value - clefCenter);
            int highDistance = Math.Abs(sortedNotes[sortedNotes.Length - 1].value - clefCenter);

            if (lowDistance > highDistance)
                direction = NoteDirection.Up;

            return direction;
        }

        public List<SpriteRenderer> CreateChord(ABC.Note[] notes, ABC.Clef clef, GameObject container, Vector3 offset)
        {
            var sortedNotes = new ABC.Note[notes.Length];
            Array.Copy(notes, sortedNotes, notes.Length);
            Array.Sort(sortedNotes);

            var items = new List<SpriteRenderer>();

            var noteDirection = DetermineChordNoteDirection(sortedNotes, clef);

            items.Add(CreateNote(sortedNotes[0], clef, container, offset, noteDirection));

            bool needsAdjustment = false;
            for (int i = 1; i < sortedNotes.Length; i++)
            {
                // If the note will not fit on the current line because there is a note right below it, then need to draw a chord dot
                if (i % 2 == 1 && sortedNotes[i].value - sortedNotes[i-1].value == 1)
                {
                    items.Add(AddChordDot(sortedNotes[i], clef, noteDirection, container, offset));
                    needsAdjustment = noteDirection == NoteDirection.Down;
                }
                else
                {
                    items.Add(CreateNote(sortedNotes[i], clef, container, offset, noteDirection));
                }
            }

            // note that when the stem direction is down then the dot will be placed too close to the previous note.
            // In this case we will push the chord over such that its minx lines up with the caret.
            if (needsAdjustment)
            {
                foreach (var item in items)
                    item.transform.localPosition = item.transform.localPosition + new Vector3(chordDotOffset, 0.0f, 0.0f);
            }
            
            return items;
        }

        SpriteRenderer CreateStaffMark(int stepCount, GameObject container, Vector3 offset)
        {
            var mark = spriteCache.GetSpriteObject("Staff_Mark");
            mark.transform.parent = container.transform;
            mark.transform.localPosition = offset + new Vector3(0.0f, noteStep * stepCount, 0.0f);

            return mark;
        }
    }
}