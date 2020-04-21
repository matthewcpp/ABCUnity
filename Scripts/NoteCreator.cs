using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;

namespace ABCUnity
{
    class NoteCreator
    {
        SpriteCache spriteCache;

        public NoteCreator(SpriteCache spriteCache)
        {
            this.spriteCache = spriteCache;
        }

        public static readonly Dictionary<ABC.Clef, ABC.Pitch> clefZero = new Dictionary<ABC.Clef, ABC.Pitch>()
        {
            { ABC.Clef.Treble, ABC.Pitch.F4}, { ABC.Clef.Bass, ABC.Pitch.A2}
        };

        public enum NoteDirection
        {
            Down, Up
        }

        /// <summary> The distance between note values on the staff. </summary>
        public const float noteStep = 0.28f;

        /// <summary> The distance to offset the Chord dot from its root note.  
        /// If the stem direction is down this value will need to be negated. 
        /// </summary>
        const float chordDotOffset = 0.67f;

        /// <summary> The distance to offset notes by if they have a mark.  This will ensure they are centered. </summary>
        const float notePadding = 0.14f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool NeedsStaffMarkers(int stepCount)
        {
            return stepCount < -2 || stepCount > 8;
        }

        public NoteInfo CreateNote(ABC.Note note, Beam beam, GameObject container, Vector3 offset)
        {
            int stepCount = note.pitch - clefZero[beam.clef];
            return CreateNote(note, stepCount, beam.stemHeight, beam.noteDirection, container, offset);
        }

        public NoteInfo CreateNote(ABC.Note note, ABC.Clef clef, GameObject container, Vector3 offset)
        {
            int stepCount = note.pitch - clefZero[clef];
            var noteDirection = stepCount > 3 ? NoteDirection.Down : NoteDirection.Up;

            return CreateNote(note, stepCount, 0.0f, noteDirection, container, offset);
        }

        public NoteInfo CreateChord(ABC.Chord chord, Beam beam, GameObject container, Vector3 offset)
        {
            return CreateChord(chord, beam.clef, beam.stemHeight, container, offset);
        }

        public NoteInfo CreateChord(ABC.Chord chord, ABC.Clef clef, GameObject container, Vector3 offset)
        {
            return CreateChord(chord, clef, 0.0f, container, offset);
        }

        /// <summary>
        /// Draws all required staff markers for a note  with a given step count from the Staff's zero value.
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

        private NoteInfo CreateNote(ABC.Note note, int noteStepCount, float stemHeight, NoteDirection noteDirection, GameObject container, Vector3 offset)
        {
            var notePosition = offset + new Vector3(0.0f, noteStep * noteStepCount, 0.0f);

            if (note.accidental != ABC.Accidental.Unspecified)
            {
                offset = offset + new Vector3(accidentalOffset, 0.0f, 0.0f);
                notePosition = notePosition + new Vector3(accidentalOffset, 0.0f, 0.0f);

                var accidental = spriteCache.GetSpriteObject($"Accidental_{note.accidental}");
                var accidentalPos = notePosition + new Vector3(-accidentalWidth, 0.0f, 0.0f);
                accidental.transform.parent = container.transform;
                accidental.transform.localPosition = accidentalPos;
            }

            GameObject staffMarkers = null;
            if (NeedsStaffMarkers(noteStepCount))
            {
                staffMarkers = new GameObject();
                AddNoteStaffMarkers(noteStepCount, staffMarkers, offset, 1.0f);
            }

            if (staffMarkers != null) // this ensures that the note appears centered w.r.t the markers
                notePosition = notePosition + new Vector3(notePadding, 0.0f, 0.0f);

            Bounds bounds;
            SpriteRenderer rootItem = null;
            if (stemHeight != 0.0f)
            {
                var noteHead = spriteCache.GetSpriteObject("Chord_Quarter");
                noteHead.transform.parent = container.transform;
                noteHead.transform.localPosition = notePosition;
                bounds = noteHead.bounds;

                rootItem = spriteCache.GetSpriteObject($"Note_Stem_{noteDirection}");
                rootItem.transform.parent = container.transform;

                var stemPos = notePosition + (noteDirection == NoteDirection.Up ? Beam.stemUpOffset : Beam.stemDownOffset);
                rootItem.transform.localPosition = stemPos;
                rootItem.transform.localScale = new Vector3(1.0f, Mathf.Abs(stemHeight - stemPos.y), 1.0f);
                bounds.Encapsulate(rootItem.bounds);
            }
            else
            {
                var spriteName = GetNoteSpriteName(note.length, note.beam != 0, noteDirection);
                rootItem = spriteCache.GetSpriteObject(spriteName);
                rootItem.transform.parent = container.transform;
                rootItem.transform.localPosition = notePosition;

                bounds = rootItem.bounds;
            }

            if (staffMarkers != null)
                staffMarkers.transform.parent = container.transform;

            return new NoteInfo(rootItem, bounds);
        }

        private string GetNoteSpriteName(ABC.Length note, bool beam, NoteDirection noteDirection)
        {
            if (beam)
                return $"Note_Quarter_{noteDirection}";
            else
                return note == ABC.Length.Whole ? "Note_Whole" : $"Note_{note}_{noteDirection}";
        }

        private SpriteRenderer AddChordNote(ABC.Pitch value, ABC.Length length, NoteDirection noteDirection, float stemHeight, bool beam, ABC.Clef clef, GameObject container, Vector3 offset, List<SpriteRenderer> items)
        {
            int stepCount = value - clefZero[clef];
            var notePosition = offset + new Vector3(0.0f, noteStep * stepCount, 0.0f);

            SpriteRenderer rootItem = null;
            if (stemHeight != 0.0f)
            {
                var noteHead = spriteCache.GetSpriteObject("Chord_Quarter");
                noteHead.transform.parent = container.transform;
                noteHead.transform.localPosition = notePosition;
                items.Add(noteHead);

                rootItem = spriteCache.GetSpriteObject($"Note_Stem_{noteDirection}");
                rootItem.transform.parent = container.transform;

                var stemPos = notePosition + (noteDirection == NoteDirection.Up ? Beam.stemUpOffset : Beam.stemDownOffset);
                rootItem.transform.localPosition = stemPos;
                rootItem.transform.localScale = new Vector3(1.0f, Mathf.Abs(stemHeight - stemPos.y), 1.0f);
            }
            else
            {
                var spriteName = GetNoteSpriteName(length, beam, noteDirection);
                rootItem = spriteCache.GetSpriteObject(spriteName);
                rootItem.transform.parent = container.transform;
                rootItem.transform.localPosition = notePosition;
            }

            items.Add(rootItem);
            return rootItem;
        }

        private SpriteRenderer AddChordDot(ABC.Pitch value, ABC.Length length, ABC.Clef clef, NoteDirection noteDirection, GameObject container, Vector3 offset)
        {
            int stepCount = value - clefZero[clef];

            var notePos = new Vector3(noteDirection == NoteDirection.Up ? chordDotOffset : -chordDotOffset, noteStep * stepCount, 0.0f);

            var spriteName = length == ABC.Length.Whole ? "Note_Whole" : $"Chord_{length}";
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
        private NoteDirection DetermineChordNoteDirection(ABC.Chord.Element[] sortedNotes, ABC.Clef clef)
        {
            var direction = NoteDirection.Down;

            var clefCenter = clefZero[clef] + 3;
            int lowDistance = Math.Abs(sortedNotes[0].pitch - clefCenter);
            int highDistance = Math.Abs(sortedNotes[sortedNotes.Length - 1].pitch - clefCenter);

            if (lowDistance > highDistance)
                direction = NoteDirection.Up;

            return direction;
        }

        private bool ChordHasDots(ABC.Chord.Element[] sortedNotes)
        {
            for (int i = 1; i < sortedNotes.Length; i++)
            {
                // If the note will not fit on the current line because there is a note right below it, then need to draw a chord dot
                if (i % 2 == 1 && sortedNotes[i].pitch - sortedNotes[i - 1].pitch == 1)
                    return true;
            }

            return false;
        }

        const int chordAccidentalSize = 6;

        public static List<List<ABC.Chord.Element>> ComputeChordAccidentalLevels(ABC.Chord.Element[] notes, ABC.Pitch clefZero)
        {
            List<int> stepLevels = null;
            List<List<ABC.Chord.Element>> notesInLevel = null;

            foreach (var note in notes)
            {
                if (note.accidental == ABC.Accidental.Unspecified)
                    continue;

                if (stepLevels == null)
                {
                    stepLevels = new List<int>();
                    notesInLevel = new List<List<ABC.Chord.Element>>();
                }

                int stepCount = note.pitch - clefZero;

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
                var newLevelNotes = new List<ABC.Chord.Element>();
                newLevelNotes.Add(note);
                notesInLevel.Add(newLevelNotes);

                stepLevels.Add(stepCount);

            //proceed to next note
            NoteProcessed:;
            }

            return notesInLevel;
        }

        private void CreateChordAccidentals(ABC.Chord.Element[] notes, ABC.Clef clef, ref Vector3 offset, GameObject container, List<SpriteRenderer> items)
        {
            var accidentalLevels = ComputeChordAccidentalLevels(notes, clefZero[clef]);

            if (accidentalLevels == null)
                return;

            offset = offset + new Vector3(-accidentalWidth, 0.0f, 0.0f);

            for (int i = accidentalLevels.Count - 1; i >= 0; i--)
            {
                foreach (var note in accidentalLevels[i])
                {
                    int stepCount = note.pitch - clefZero[clef];

                    var accidental = spriteCache.GetSpriteObject($"Accidental_{note.accidental}");
                    accidental.transform.parent = container.transform;
                    accidental.transform.localPosition = offset + new Vector3(0.0f, noteStep * stepCount, 0.0f);
                    items.Add(accidental);
                }

                offset = offset + new Vector3(accidentalWidth, 0.0f, 0.0f);
            }
        }

        public struct NoteInfo
        {
            public NoteInfo(SpriteRenderer root, Bounds bounding)
            {
                this.root = root;
                this.bounding = bounding;
            }

            public SpriteRenderer root;
            public Bounds bounding;
        }

        Bounds CalculateBoundsForItems(List<SpriteRenderer> items)
        {
            Bounds b = items[0].bounds;

            for (int i = 0; i < items.Count; i++)
                b.Encapsulate(items[i].bounds);

            return b;
        }

        private NoteInfo CreateChord(ABC.Chord chord, ABC.Clef clef, float stemHeight, GameObject container, Vector3 offset)
        {
            var items = new List<SpriteRenderer>();

            var noteDirection = DetermineChordNoteDirection(chord.notes, clef);
            float staffMarkerScale = 1.0f;

            CreateChordAccidentals(chord.notes, clef, ref offset, container, items);

            if (ChordHasDots(chord.notes))
            {
                staffMarkerScale = 2.0f;

                // note that when the stem direction is down then the dot will be placed too close to the previous note.
                // In this case we will push the chord over such that its min x value lines up with the caret.
                if (noteDirection == NoteDirection.Down)
                    offset = offset + new Vector3(chordDotOffset, 0.0f, 0.0f);
            }

            GameObject staffMarkers = null;
            if (NeedsStaffMarkers(chord.notes[0].pitch - clefZero[clef]) || NeedsStaffMarkers(chord.notes[chord.notes.Length - 1].pitch - clefZero[clef]))
            {
                staffMarkers = new GameObject();
                
                AddNoteStaffMarkers(chord.notes[0].pitch - clefZero[clef], staffMarkers, offset, staffMarkerScale);
                AddNoteStaffMarkers(chord.notes[chord.notes.Length - 1].pitch - clefZero[clef], staffMarkers, offset,
                    staffMarkerScale);
            }

            if (staffMarkers != null) // this ensures that the note appears centered w.r.t the markers
                offset = offset + new Vector3(notePadding, 0.0f, 0.0f);

            var rootItem = AddChordItems(chord, chord.length, noteDirection, stemHeight, clef, chord.beam != 0, container, offset, items);
            
            if (staffMarkers != null)
                staffMarkers.transform.parent = container.transform;

            return new NoteInfo(rootItem, CalculateBoundsForItems(items));
        }

        private SpriteRenderer AddChordItems(ABC.Chord chord, ABC.Length length, NoteDirection noteDirection, float stemHeight, ABC.Clef clef, bool beam, GameObject container, Vector3 offset, List<SpriteRenderer> items)
        {
            bool[] stems = new bool[chord.notes.Length];
            SpriteRenderer rootItem = null;

            var dotValue = length > ABC.Length.Quarter ? length : ABC.Length.Quarter;
            var noteValue = beam ? ABC.Length.Quarter : length;

            if (noteDirection == NoteDirection.Down)
            {
                for (int i = 0; i < chord.notes.Length; i++)
                {
                    if (i > 0 && stems[i - 1] == true && chord.notes[i].pitch - chord.notes[i - 1].pitch == 1)
                        items.Add(AddChordDot(chord.notes[i].pitch, dotValue, clef, noteDirection, container, offset));
                    else
                    {
                        var root = AddChordNote(chord.notes[i].pitch, noteValue, noteDirection, stemHeight, chord.beam != 0, clef, container, offset, items);
                        stems[i] = true;
                        noteValue = dotValue;

                        if (rootItem == null)
                            rootItem = root;
                    }
                }
            }
            else
            {
                for (int i = chord.notes.Length - 1; i >= 0; i--)
                {
                    if (i != chord.notes.Length - 1 && stems[i + 1] == true && chord.notes[i + 1].pitch - chord.notes[i].pitch == 1)
                        items.Add(AddChordDot(chord.notes[i].pitch, dotValue, clef, noteDirection, container, offset));
                    else
                    {
                        var root = AddChordNote(chord.notes[i].pitch, noteValue, noteDirection, stemHeight, chord.beam != 0, clef, container, offset, items);
                        stems[i] = true;
                        noteValue = dotValue;

                        if (rootItem == null)
                            rootItem = items[items.Count - 1];
                    }
                }
            }

            return rootItem;
        }

        private SpriteRenderer CreateStaffMark(int stepCount, GameObject container, Vector3 offset, float localScaleX)
        {
            var mark = spriteCache.GetSpriteObject("Staff_Mark");
            mark.transform.parent = container.transform;
            mark.transform.localPosition = offset + new Vector3(0.0f, noteStep * stepCount, 0.0f);
            mark.transform.localScale = new Vector3(localScaleX, 1.0f, 1.0f);

            return mark;
        }

        static readonly Dictionary<ABC.Length, float> restHeight = new Dictionary<ABC.Length, float>()
        {
            { ABC.Length.Whole, 1.41f }, { ABC.Length.Half, 1.16f }, { ABC.Length.Quarter, 0.3f}, { ABC.Length.Eighth, 0.0f}, { ABC.Length.Sixteenth, 0.0f }
        };

        public SpriteRenderer CreateRest(ABC.Rest rest, GameObject container, Vector3 offset)
        {
            var restSprite = rest.length == ABC.Length.Whole ? "Rest_Half" : $"Rest_{rest.length}";
            var restObj = spriteCache.GetSpriteObject(restSprite);
            restObj.transform.parent = container.transform;
            restObj.transform.localPosition = offset + new Vector3(0.0f, restHeight[rest.length], 0.0f);

            return restObj;
        }
    }
    }