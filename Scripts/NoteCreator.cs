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
            Down, Up
        }

        /// <summary> The distance between note values on the staff. </summary>
        const float noteStep = 0.28f;

        /// <summary> The distance to offset the Chord dot from its root note.  
        /// If the stem direction is down this value will need to be negated. 
        /// </summary>
        const float chordDotOffset = 0.67f;

        /// <summary> The distance to offset notes by if they have a mark.  This will ensure they are centered. </summary>
        const float notePadding = 0.14f;

        /// <summary>
        /// Draws all required staff markers for a note  with a given stepcount from the Staff's zero value.
        /// </summary>
        private bool AddNoteStaffMarkers(int stepCount, GameObject container, Vector3 offset, float localScaleX)
        {
            if (stepCount < -2)  // below the staff
            {
                int stepOffset = stepCount % 2 == 0 ? 1 : 0;

                // if step offset is zero then the mark is though the middle of the note
                CreateStaffMark(stepCount + stepOffset, container, offset, stepOffset == 0 ? 1.0f : localScaleX);

                for (int sc = stepCount + 2 + stepOffset; sc < -2; sc += 2)
                    CreateStaffMark(sc, container, offset, localScaleX);

                return true;
            }
            else if (stepCount > 8) // above the staff
            {
                if (stepCount % 2 == 0) // mark belongs above
                    CreateStaffMark(stepCount - 1, container, offset, localScaleX);
                else // mark in middle
                    CreateStaffMark(stepCount, container, offset, 1.0f);

                for (int sc = stepCount - 2; sc > 8; sc -= 2)
                    CreateStaffMark(sc, container, offset, localScaleX);

                return true;
            }

            return false;
        }

        const float accidentalOffset = 0.25f;
        const float accidentalWidth = 0.55f;

        public SpriteRenderer CreateNote(ABC.Note note, ABC.Clef clef, GameObject container, Vector3 offset)
        {
            int stepCount = note.value - clefZero[clef];
            var noteDirection = stepCount > 3 ? NoteDirection.Down : NoteDirection.Up;
            var notePosition = offset + new Vector3(0.0f, noteStep * stepCount, 0.0f);

            if (note.accidental != ABC.Note.Accidental.Unspecified)
            {
                offset = offset + new Vector3(accidentalOffset, 0.0f, 0.0f);
                notePosition = notePosition + new Vector3(accidentalOffset, 0.0f, 0.0f);

                var accidental = spriteCache.GetSpriteObject($"Accidental_{note.accidental}");
                var accidentalPos = notePosition + new Vector3(-accidentalWidth, 0.0f, 0.0f);
                accidental.transform.parent = container.transform;
                accidental.transform.localPosition = accidentalPos;
            }

            bool addedMarkers = AddNoteStaffMarkers(stepCount, container, offset, 1.0f);

            if (addedMarkers)
                notePosition = notePosition + new Vector3(notePadding, 0.0f, 0.0f);

            var spriteName = note.length == ABC.Note.Length.Whole ? "Note_Whole" : $"Note_{note.length}_{noteDirection}";
            var noteObj = spriteCache.GetSpriteObject(spriteName);
            noteObj.transform.parent = container.transform;
            noteObj.transform.localPosition = notePosition;

            return noteObj;
        }

        private SpriteRenderer AddChordNote(ABC.Note.Value value, ABC.Note.Length length, NoteDirection noteDirection, ABC.Clef clef, GameObject container, Vector3 offset)
        {
            int stepCount = value - clefZero[clef];
            var notePosition = offset + new Vector3(0.0f, noteStep * stepCount, 0.0f);

            var spriteName = length == ABC.Note.Length.Whole ? "Note_Whole" : $"Note_{length}_{noteDirection}";
            var noteObj = spriteCache.GetSpriteObject(spriteName);
            noteObj.transform.parent = container.transform;
            noteObj.transform.localPosition = notePosition;

            return noteObj;
        }

        private SpriteRenderer AddChordDot(ABC.Note.Value value, ABC.Note.Length length, ABC.Clef clef, NoteDirection noteDirection, GameObject container, Vector3 offset)
        {
            int stepCount = value - clefZero[clef];

            var notePos = new Vector3(noteDirection == NoteDirection.Up ? chordDotOffset : -chordDotOffset, noteStep * stepCount, 0.0f);

            var spriteName = length == ABC.Note.Length.Whole ? "Note_Whole" : $"Chord_{length}";
            var dot = spriteCache.GetSpriteObject(spriteName);
            dot.transform.parent = container.transform;
            dot.transform.localPosition = offset + notePos;

            return dot;
        }

        /// <summary>
        /// The direction of the chord stem is chosen to minimize stem length outside the staff.
        /// The direction is chosen by examining the extreme notes of the chord and picking the direction based on the note which is farthest away from the value of the middle staffline.
        /// Precondition: notes array should be sorted in ascending order.
        /// </summary>
        private NoteDirection DetermineChordNoteDirection(ABC.Note[] sortedNotes, ABC.Clef clef)
        {
            var direction = NoteDirection.Down;

            var clefCenter = clefZero[clef] + 3;
            int lowDistance = Math.Abs(sortedNotes[0].value - clefCenter);
            int highDistance = Math.Abs(sortedNotes[sortedNotes.Length - 1].value - clefCenter);

            if (lowDistance > highDistance)
                direction = NoteDirection.Up;

            return direction;
        }

        private bool ChordHasDots(ABC.Note[] sortedNotes)
        {
            for (int i = 1; i < sortedNotes.Length; i++)
            {
                // If the note will not fit on the current line because there is a note right below it, then need to draw a chord dot
                if (i % 2 == 1 && sortedNotes[i].value - sortedNotes[i - 1].value == 1)
                    return true;
            }

            return false;
        }

        const int chordAccidentalSize = 6;

        public static List<List<ABC.Note>> ComputeChordAccidentalLevels(ABC.Note[] notes, ABC.Note.Value clefZero)
        {
            List<int> stepLevels = null;
            List<List<ABC.Note>> notesInLevel = null;

            foreach (var note in notes)
            {
                if (note.accidental == ABC.Note.Accidental.Unspecified)
                    continue;

                if (stepLevels == null)
                {
                    stepLevels = new List<int>();
                    notesInLevel = new List<List<ABC.Note>>();
                }

                int stepCount = note.value - clefZero;

                // place this accidental into the first level it will fit
                for (int i = 0; i < stepLevels.Count; i++)
                {
                    if (stepCount - stepLevels[i] > chordAccidentalSize)
                    {
                        stepLevels[i] = stepCount;
                        notesInLevel[i].Add(note);

                        goto NoteProcessed;
                    }
                }

                //Could not fit in an existing level, create a new one
                var newLevelNotes = new List<ABC.Note>();
                newLevelNotes.Add(note);
                notesInLevel.Add(newLevelNotes);

                stepLevels.Add(stepCount);

                //proceed to next note
                NoteProcessed:;
            }

            return notesInLevel;
        }

        private void CreateChordAccidentals(ABC.Note[] notes, ABC.Clef clef, ref Vector3 offset, GameObject container, List<SpriteRenderer> items)
        {
            var accidentalLevels = ComputeChordAccidentalLevels(notes, clefZero[clef]);

            if (accidentalLevels == null)
                return;

            offset = offset + new Vector3(-accidentalWidth, 0.0f, 0.0f);

            for (int i = accidentalLevels.Count - 1; i >= 0; i--)
            {
                foreach (var note in accidentalLevels[i])
                {
                    int stepCount = note.value - clefZero[clef];

                    var accidental = spriteCache.GetSpriteObject($"Accidental_{note.accidental}");
                    accidental.transform.parent = container.transform;
                    accidental.transform.localPosition = offset + new Vector3(0.0f, noteStep * stepCount, 0.0f);
                    items.Add(accidental);
                }

                offset = offset + new Vector3(accidentalWidth, 0.0f, 0.0f);
            }
        }

        public List<SpriteRenderer> CreateChord(ABC.Note[] notes, ABC.Clef clef, GameObject container, Vector3 offset)
        {
            var sortedNotes = new ABC.Note[notes.Length];
            Array.Copy(notes, sortedNotes, notes.Length);
            Array.Sort(sortedNotes);

            var items = new List<SpriteRenderer>();

            var noteDirection = DetermineChordNoteDirection(sortedNotes, clef);
            float staffMarkerScale = 1.0f;

            CreateChordAccidentals(notes, clef, ref offset, container, items);

            if (ChordHasDots(notes))
            {
                staffMarkerScale = 2.0f;

                // note that when the stem direction is down then the dot will be placed too close to the previous note.
                // In this case we will push the chord over such that its min x value lines up with the caret.
                if (noteDirection == NoteDirection.Down)
                    offset = offset + new Vector3(chordDotOffset, 0.0f, 0.0f);
            } 

            // TODO: if both below only need to do lowest
            bool hasMarkers = AddNoteStaffMarkers(notes[0].value - clefZero[clef], container, offset, staffMarkerScale) || 
                AddNoteStaffMarkers(notes[notes.Length - 1].value - clefZero[clef], container, offset, staffMarkerScale);

            if (hasMarkers)
                offset = offset + new Vector3(notePadding, 0.0f, 0.0f);

            AddChordItems(sortedNotes, noteDirection, clef, container, offset, items);

            return items;
        }

        private void AddChordItems(ABC.Note[] sortedNotes, NoteDirection noteDirection, ABC.Clef clef, GameObject container, Vector3 offset, List<SpriteRenderer> items)
        {
            var dotValue = sortedNotes[0].length < ABC.Note.Length.Quarter ? sortedNotes[0].length : ABC.Note.Length.Quarter;
            var noteValue = sortedNotes[0].length;

            if (noteDirection == NoteDirection.Down)
            {
                for (int i = 0; i < sortedNotes.Length; i++)
                {
                    if (i > 0 && sortedNotes[i].value - sortedNotes[i - 1].value == 1)
                        items.Add(AddChordDot(sortedNotes[i].value, dotValue, clef, noteDirection, container, offset));
                    else
                    {
                        items.Add(AddChordNote(sortedNotes[i].value, noteValue, noteDirection, clef, container, offset));
                        noteValue = dotValue;
                    }
                }
            }
            else
            {
                for (int i = sortedNotes.Length - 1; i >= 0; i--)
                {
                    if (i > 0 && sortedNotes[i].value - sortedNotes[i - 1].value == 1)
                        items.Add(AddChordDot(sortedNotes[i].value, dotValue, clef, noteDirection, container, offset));
                    else
                    {
                        items.Add(AddChordNote(sortedNotes[i].value, noteValue, noteDirection, clef, container, offset));
                        noteValue = dotValue;
                    }
                }
            }
        }

        private SpriteRenderer CreateStaffMark(int stepCount, GameObject container, Vector3 offset, float localScaleX)
        {
            var mark = spriteCache.GetSpriteObject("Staff_Mark");
            mark.transform.parent = container.transform;
            mark.transform.localPosition = offset + new Vector3(0.0f, noteStep * stepCount, 0.0f);
            mark.transform.localScale = new Vector3(localScaleX, 1.0f, 1.0f);

            return mark;
        }
    }
}