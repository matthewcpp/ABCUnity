﻿using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;
using ABC;

namespace ABCUnity
{
    public struct NoteInfo
    {
        public NoteInfo(Bounds rootBounding, Bounds bounding)
        {
            this.rootBounding = rootBounding;
            this.totalBounding = bounding;
        }

        public Bounds rootBounding;
        public Bounds totalBounding;
    }
    
    class NoteCreator
    {
        private SpriteCache spriteCache;

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
        const float compressedChordNoteOffset = 0.67f;

        const float compressedChordWholeNoteOffset = 0.75f;

        /// <summary> The distance to offset notes by if they have a mark.  This will ensure they are centered. </summary>
        const float staffMarkerNoteOffset = 0.14f;

        /// <summary> Distance between dots </summary>
        const float dotAdvance = 0.2f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool NeedsStaffMarkers(int stepCount)
        {
            return stepCount < -2 || stepCount > 8;
        }

        public NoteInfo CreateNote(ABC.Note note, Beam beam, IReadOnlyList<string> decorations, GameObject container)
        {
            int stepCount = note.pitch - clefZero[beam.clef];
            return CreateNote(note, stepCount, beam.stemHeight, beam.noteDirection, decorations, container);
        }

        public NoteInfo CreateNote(ABC.Note note, ABC.Clef clef, IReadOnlyList<string> decorations, GameObject container)
        {
            int stepCount = note.pitch - clefZero[clef];
            var noteDirection = stepCount > 3 ? NoteDirection.Down : NoteDirection.Up;

            return CreateNote(note, stepCount, 0.0f, noteDirection, decorations, container);
        }

        public NoteInfo CreateChord(ABC.Chord chord, Beam beam, IReadOnlyList<string> decorations, GameObject container)
        {
            return CreateChord(chord, beam.clef, beam.noteDirection, beam.stemHeight, decorations, container);
        }

        public NoteInfo CreateChord(ABC.Chord chord, ABC.Clef clef, IReadOnlyList<string> decorations, GameObject container)
        {
            var noteDirection = DetermineChordNoteDirection(chord.notes, clef);
            if (chord.length == ABC.Length.Whole)
                return CreateWholeNoteChord(chord, clef, decorations, container);
            else
                return CreateChord(chord, clef, noteDirection, 0.0f, decorations, container);
        }

        /// <summary>
        /// Draws all required staff markers for a note  with a given step count from the Staff's zero value.
        /// </summary>
        private bool AddNoteStaffMarkers(int stepCount, GameObject container, float positionX, float localScaleX, ref Bounds bounding)
        {
            if (stepCount < -2)  // below the staff
            {
                int stepOffset = stepCount % 2 == 0 ? 1 : 0;

                // if the step count is odd then the mark belongs in the middle, else below
                bounding.Encapsulate(CreateStaffMark(stepCount + stepOffset, container, positionX, stepOffset == 0 ? 1.0f : localScaleX));

                for (int sc = stepCount + 2 + stepOffset; sc < -2; sc += 2)
                    bounding.Encapsulate(CreateStaffMark(sc, container, positionX, localScaleX));

                return true;
            }
            else if (stepCount > 8) // above the staff
            {
                int stepOffset = stepCount % 2 == 0 ? 1 : 0;

                // if the step count is odd then the mark belongs in the middle, else above
                bounding.Encapsulate(CreateStaffMark(stepCount + stepOffset, container, positionX, 1.0f));

                for (int sc = stepCount + stepOffset - 2; sc > 8; sc -= 2)
                    bounding.Encapsulate(CreateStaffMark(sc, container, positionX, stepOffset == 0 ? 1.0f : localScaleX));

                return true;
            }

            return false;
        }

        /// <summary> The space between an accidental and the note it is attached to. </summary>
        const float accidentalOffset = 0.25f;
        const float accidentalWidth = 0.55f;

        private NoteInfo CreateNote(ABC.Note note, int noteStepCount, float stemHeight, NoteDirection noteDirection, IReadOnlyList<string> decorations, GameObject container)
        {
            var notePosition =  new Vector3(0.0f, noteStep * noteStepCount, 0.0f);

            var totalBounds = new Bounds();
            totalBounds.SetMinMax(Vector3.zero, Vector3.zero);
            if (note.accidental != ABC.Accidental.Unspecified)
            {
                var accidental = spriteCache.GetSpriteObject($"Accidental_{note.accidental}");
                accidental.transform.parent = container.transform;
                accidental.transform.localPosition = notePosition;

                totalBounds.Encapsulate(accidental.bounds);
                notePosition.x = totalBounds.size.x + accidentalOffset;
            }

            GameObject staffMarkers = null;
            if (NeedsStaffMarkers(noteStepCount))
            {
                staffMarkers = new GameObject();
                AddNoteStaffMarkers(noteStepCount, staffMarkers, notePosition.x, 1.0f, ref totalBounds);
                notePosition += new Vector3(staffMarkerNoteOffset, 0.0f, 0.0f);
            }

            Bounds rootItemBounds;
            SpriteRenderer rootItem = null;
            if (stemHeight != 0.0f)
            {
                var noteHead = spriteCache.GetSpriteObject("Chord_Quarter");
                noteHead.transform.parent = container.transform;
                noteHead.transform.localPosition = notePosition;
                rootItemBounds = noteHead.bounds;

                rootItem = spriteCache.GetSpriteObject($"Note_Stem_{noteDirection}");
                rootItem.transform.parent = container.transform;

                var stemPos = notePosition + (noteDirection == NoteDirection.Up ? Beam.stemUpOffset : Beam.stemDownOffset);
                rootItem.transform.localPosition = stemPos;
                rootItem.transform.localScale = new Vector3(1.0f, Mathf.Abs(stemHeight - stemPos.y), 1.0f);
                rootItemBounds.Encapsulate(rootItem.bounds);
            }
            else
            {
                var spriteName = GetNoteSpriteName(note.length, note.beam != 0, noteDirection);
                rootItem = spriteCache.GetSpriteObject(spriteName);
                rootItem.transform.parent = container.transform;
                rootItem.transform.localPosition = notePosition;

                rootItemBounds = rootItem.bounds;
            }

            totalBounds.Encapsulate(rootItemBounds);

            for (int i = 0; i < note.dotCount; i++)
            {
                float dotOffset = rootItemBounds.max.x + dotAdvance;
                var dot = CreateNoteDot(noteStepCount, container, dotOffset);
                totalBounds.Encapsulate(dot.bounds);
            }

            AddFingeringDecorations(note, decorations, rootItemBounds, container, ref totalBounds);

            if (staffMarkers != null)
                staffMarkers.transform.parent = container.transform;

            return new NoteInfo(rootItemBounds, totalBounds);
        }

        const float minDecorationHeight = 2.5f;

        private static HashSet<string> fingeringDecorations = new HashSet<string>()
        {
            "1", "2", "3", "4", "5"
        };

        private void AddFingeringDecorations(ABC.Item item, IReadOnlyList<string> decorations, Bounds referenceBounding, GameObject container, ref Bounds bounds)
        {
            if (decorations != null)
            {
                string decorationText = "";
                foreach (var decoration in decorations)
                {
                    if (fingeringDecorations.Contains(decoration))
                    {
                        if (decorationText.Length > 0)
                            decorationText += '\n';
                        decorationText += decoration;
                    }
                }

                if (decorationText.Length > 0)
                {
                    var text = spriteCache.GetTextObject();
                    text.text = decorationText;
                    text.transform.parent = container.transform;

                    var decorationHeight = Mathf.Max(referenceBounding.max.y + 0.2f, minDecorationHeight);
                    var textPos = new Vector3(referenceBounding.min.x, decorationHeight, 0.0f);
                    text.transform.position = textPos;

                    text.ForceMeshUpdate();

                    Bounds b = new Bounds();
                    b.SetMinMax(textPos, textPos + text.textBounds.size);

                    bounds.Encapsulate(b);
                }
            }
        }


        private string GetNoteSpriteName(ABC.Length note, bool beam, NoteDirection noteDirection)
        {
            if (beam)
                return $"Note_Quarter_{noteDirection}";
            else
                return note == ABC.Length.Whole ? "Note_Whole" : $"Note_{note}_{noteDirection}";
        }

        private Bounds AddChordNoteHead(ABC.Pitch value, ABC.Length length, ABC.Clef clef, NoteDirection noteDirection, GameObject container, Vector3 offset, ref Vector3 notePos)
        {
            int stepCount = value - clefZero[clef];

            notePos = offset + new Vector3(0.0f, noteStep * stepCount, 0.0f);

            var spriteName = length == ABC.Length.Whole ? "Note_Whole" : $"Chord_{length}";
            var dot = spriteCache.GetSpriteObject(spriteName);
            dot.transform.parent = container.transform;
            dot.transform.localPosition = notePos;

            return dot.bounds;
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

        private void CreateChordAccidentals(ABC.Chord.Element[] notes, ABC.Clef clef, ref Vector3 offset, GameObject container, ref Bounds bounding)
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
                    bounding.Encapsulate(accidental.bounds);
                }

                offset = offset + new Vector3(accidentalWidth, 0.0f, 0.0f);
            }
        }

        private NoteInfo CreateWholeNoteChord(ABC.Chord chord, ABC.Clef clef, IReadOnlyList<string> decorations, GameObject container)
        {
            var offset = Vector3.zero;
            var totalBounds = new Bounds();
            totalBounds.SetMinMax(offset, offset);

            CreateChordAccidentals(chord.notes, clef, ref offset, container, ref totalBounds);
            offset.x = totalBounds.max.x;

            var staffMarkers = CreateChordStaffMarkersDown(chord, clef, offset.x, compressedChordWholeNoteOffset,ref totalBounds);

            if (staffMarkers == null)
                staffMarkers = CreateChordStaffMarkersUp(chord, clef, offset.x, compressedChordWholeNoteOffset, ref totalBounds);

            if (staffMarkers != null) // this ensures that the note appears centered w.r.t the markers
                offset += new Vector3(staffMarkerNoteOffset, 0.0f, 0.0f);

            var rootBounds = CreateWholeNoteChordNotes(chord, clef, container, offset, ref totalBounds);

            if (staffMarkers != null)
                staffMarkers.transform.parent = container.transform;

            return new NoteInfo(rootBounds, totalBounds);
        }

        private NoteInfo CreateChord(ABC.Chord chord, ABC.Clef clef, NoteDirection noteDirection, float stemHeight, IReadOnlyList<string> decorations, GameObject container)
        {
            var offset = Vector3.zero;
            var totalBounds = new Bounds();
            totalBounds.SetMinMax(offset, offset);

            CreateChordAccidentals(chord.notes, clef, ref offset, container, ref totalBounds);
            offset.x = totalBounds.max.x;

            var staffMarkers = CreateChordStaffMarkers(noteDirection, chord, clef, offset.x, compressedChordNoteOffset, ref totalBounds);

            if (staffMarkers != null) // this ensures that the note appears centered w.r.t the markers
                offset += new Vector3(staffMarkerNoteOffset, 0.0f, 0.0f);

            Bounds rootBounds = CreateChordNotes(noteDirection, chord, chord.length, stemHeight, clef, chord.beam != 0, container, offset, ref totalBounds);
            AddFingeringDecorations(chord, decorations, rootBounds, container, ref totalBounds);

            if (staffMarkers != null)
                staffMarkers.transform.parent = container.transform;

            return new NoteInfo(rootBounds, totalBounds);
        }

        private GameObject CreateChordStaffMarkers(NoteDirection noteDirection, Chord chord, ABC.Clef clef, float position, float offsetSize, ref Bounds totalBounds)
        {
            if (noteDirection == NoteDirection.Up)
                return CreateChordStaffMarkersUp(chord, clef, position, offsetSize, ref totalBounds);
            else
                return CreateChordStaffMarkersDown(chord, clef, position, -offsetSize, ref totalBounds);
        }

        private GameObject CreateChordStaffMarkersUp(Chord chord, ABC.Clef clef, float position, float offsetSize, ref Bounds totalBounds)
        {
            int stepCount = chord.notes[0].pitch - clefZero[clef];

            if (stepCount > -3) 
                return null;

            if (stepCount % 2 == 0)
                stepCount += 1;

            var staffMarkers = new GameObject("Staff Markers");

            for (int step = stepCount; step <= -3; step += 2)
                totalBounds.Encapsulate(CreateStaffMark(step, staffMarkers, position, 1.0f));

            for (int i = 1; i < chord.notes.Length; i++)
            {
                if (stepCount > -3)
                    break;

                if (chord.notes[i].pitch - chord.notes[i - 1].pitch == 1)
                {
                    stepCount = chord.notes[i].pitch - clefZero[clef];
                    if (stepCount % 2 == 0)
                        stepCount += 1;

                    for (int step = stepCount; step <= -3; step += 2)
                        totalBounds.Encapsulate(CreateStaffMark(step, staffMarkers, position + offsetSize, 1.0f));
                    break;
                }
            }

            return staffMarkers;
        }

        private GameObject CreateChordStaffMarkersDown(Chord chord, ABC.Clef clef, float position, float offsetSize, ref Bounds totalBounds)
        {
            int stepCount = chord.notes[chord.notes.Length - 1].pitch - clefZero[clef];

            if (stepCount < 9)
                return null;

            if (stepCount % 2 == 0)
                stepCount -= 1;

            var staffMarkers = new GameObject("Staff Markers");

            for (int step = stepCount; step >= 9; step -= 2)
                totalBounds.Encapsulate(CreateStaffMark(step, staffMarkers, position, 1.0f));

            for (int i = chord.notes.Length - 2; i >= 0; i--)
            {
                if (stepCount < 9)
                    break;

                if (chord.notes[i + 1].pitch - chord.notes[i].pitch == 1)
                {
                    stepCount = chord.notes[i].pitch - clefZero[clef];
                    if (stepCount % 2 == 0)
                        stepCount -= 1;

                    for (int step = stepCount; step >= 9; step -= 2)
                        totalBounds.Encapsulate(CreateStaffMark(step, staffMarkers, position + offsetSize, 1.0f));
                    break;
                }
            }

            return staffMarkers;
        }

        private Bounds CreateChordNotes(NoteDirection noteDirection, ABC.Chord chord, ABC.Length length, float stemHeight, ABC.Clef clef, bool beam, GameObject container, Vector3 offset, ref Bounds totalBounds)
        {
            if (noteDirection == NoteDirection.Up)
                return CreateChordNotesUp(chord, length, stemHeight, clef, beam, container, offset, ref totalBounds);
            else
                return CreateChordNotesDown(chord, length, stemHeight, clef, beam, container, offset, ref totalBounds);
        }

        private Bounds CreateChordNotesUp(ABC.Chord chord, ABC.Length length, float stemHeight, ABC.Clef clef, bool beam, GameObject container, Vector3 offset, ref Bounds totalBounds)
        {
            var stem = spriteCache.GetSpriteObject("Note_Stem_Up");
            stem.transform.parent = container.transform;
            var lastNotePos = Vector3.zero;

            var dotValue = length > ABC.Length.Quarter ? length : ABC.Length.Quarter;
            var chordBounds = AddChordNoteHead(chord.notes[0].pitch, dotValue, clef, NoteDirection.Down, container, offset, ref lastNotePos);
            var stemPos = chordBounds.min + Beam.stemUpOffset;
            stem.transform.localPosition = stemPos;

            bool right = false;

            for (int i = 1; i < chord.notes.Length; i++)
            {
                var noteOffset = offset;
                if (!right && chord.notes[i].pitch - chord.notes[i - 1].pitch == 1)
                {
                    noteOffset.x += compressedChordNoteOffset;
                    right = true;
                }
                else
                {
                    right = false;
                }

                chordBounds.Encapsulate(AddChordNoteHead(chord.notes[i].pitch, dotValue, clef, NoteDirection.Down, container, noteOffset, ref lastNotePos));
            }

            if (stemHeight == 0.0f)
            {
                lastNotePos += Beam.stemUpOffset;
                stemHeight = Mathf.Abs(lastNotePos.y - stemPos.y) + Beam.defaultStemHeight;
            }
            else
            {
                stemHeight = stemHeight - stemPos.y;
            }

            stem.transform.localScale = new Vector3(1.0f, stemHeight, 1.0f);

            chordBounds.Encapsulate(stem.bounds);
            totalBounds.Encapsulate(chordBounds);

            return stem.bounds;
        }

        private Bounds CreateChordNotesDown(ABC.Chord chord, ABC.Length length, float stemHeight, ABC.Clef clef, bool beam, GameObject container, Vector3 offset, ref Bounds totalBounds)
        {
            var stem = spriteCache.GetSpriteObject("Note_Stem_Down");
            stem.transform.parent = container.transform;
            var lastNotePos = Vector3.zero;

            var dotValue = length > ABC.Length.Quarter ? length : ABC.Length.Quarter;
            var chordBounds = AddChordNoteHead(chord.notes[chord.notes.Length - 1].pitch, dotValue, clef, NoteDirection.Down, container, offset, ref lastNotePos);
            var stemPos = container.transform.GetChild(container.transform.childCount - 1).localPosition + Beam.stemDownOffset;
            stem.transform.localPosition = stemPos;

            bool left = false;
            
            for (int i = chord.notes.Length - 2; i >= 0; i--)
            {
                var noteOffset = offset;
                if (!left && chord.notes[i + 1].pitch - chord.notes[i].pitch == 1)
                {
                    noteOffset.x -= compressedChordNoteOffset;
                    left = true;
                }
                else
                {
                    left = false;
                }

                chordBounds.Encapsulate(AddChordNoteHead(chord.notes[i].pitch, dotValue, clef, NoteDirection.Down, container, noteOffset, ref lastNotePos));
            }

            if (stemHeight == 0.0f)
                stemHeight = Mathf.Abs((lastNotePos.y - Beam.defaultStemHeight) - stemPos.y);
            else
                stemHeight = Mathf.Abs(stemHeight - stemPos.y);

            stem.transform.localScale = new Vector3(1.0f, stemHeight, 1.0f);

            chordBounds.Encapsulate(stem.bounds);
            totalBounds.Encapsulate(chordBounds);

            return stem.bounds;
        }

        private Bounds CreateWholeNoteChordNotes(ABC.Chord chord, ABC.Clef clef, GameObject container, Vector3 offset, ref Bounds totalBounds)
        {
            var lastNotePos = Vector3.zero;
            var rootBounds = AddChordNoteHead(chord.notes[0].pitch, ABC.Length.Whole, clef, NoteDirection.Down, container, offset, ref lastNotePos);
            totalBounds.Encapsulate(rootBounds);
            bool right = false;

            for (int i = 1; i < chord.notes.Length; i++)
            {
                var noteOffset = offset;
                Bounds itemBounds;
                if (!right && chord.notes[i].pitch - chord.notes[i - 1].pitch == 1)
                {
                    noteOffset.x += compressedChordWholeNoteOffset;
                    right = true;
                }
                else
                {
                    right = false;
                }

                itemBounds = AddChordNoteHead(chord.notes[i].pitch, ABC.Length.Whole, clef, NoteDirection.Down, container, noteOffset, ref lastNotePos);
                totalBounds.Encapsulate(itemBounds);

                if (!right)
                    rootBounds.Encapsulate(itemBounds);
            }

            return rootBounds;
        }

        void AddChordDots(ABC.Chord.Element note, int dotCount, ABC.Clef clef, Bounds rootItem, GameObject container, ref Bounds totalBounds)
        {
            int stepCount = note.pitch - clefZero[clef];
            Bounds anchor = rootItem;

            for (int i = 0; i < dotCount; i++)
            {
                Vector3 dotOffset = new Vector3(anchor.max.x + dotAdvance, 0.0f, 0.0f);
                var dot = CreateNoteDot(stepCount, container, dotOffset.x);
                anchor = dot.bounds;
                totalBounds.Encapsulate(anchor);
            }
        }

        private Bounds CreateStaffMark(int stepCount, GameObject container, float positionX, float localScaleX)
        {
            var mark = spriteCache.GetSpriteObject("Staff_Mark");
            mark.transform.parent = container.transform;
            mark.transform.localPosition = new Vector3(positionX, noteStep * stepCount, 0.0f);
            mark.transform.localScale = new Vector3(localScaleX, 1.0f, 1.0f);

            return mark.bounds;
        }

        private SpriteRenderer CreateNoteDot(int stepCount, GameObject container, float positionX)
        {
            var dot = spriteCache.GetSpriteObject("Note_Dot");
            dot.transform.parent = container.transform;
            dot.transform.localPosition = new Vector3(positionX, noteStep * (stepCount + 1), 0.0f);

            return dot;
        }

        static readonly Dictionary<ABC.Length, float> restHeight = new Dictionary<ABC.Length, float>()
        {
            { ABC.Length.Whole, 1.41f }, { ABC.Length.Half, 1.16f }, { ABC.Length.Quarter, 0.3f}, { ABC.Length.Eighth, 0.0f}, { ABC.Length.Sixteenth, 0.0f }
        };

        public NoteInfo CreateRest(ABC.Rest rest, GameObject container)
        {
            var restSprite = rest.length == ABC.Length.Whole ? "Rest_Half" : $"Rest_{rest.length}";
            var restObj = spriteCache.GetSpriteObject(restSprite);
            restObj.transform.parent = container.transform;
            restObj.transform.localPosition = new Vector3(0.0f, restHeight[rest.length], 0.0f);

            return new NoteInfo(restObj.bounds, restObj.bounds);
        }

        public NoteInfo CreateMeasureRest(ABC.MultiMeasureRest rest, GameObject container)
        {
            var restObj = spriteCache.GetSpriteObject("Rest_Half");
            restObj.transform.parent = container.transform;
            restObj.transform.localPosition = new Vector3(0.0f, restHeight[ABC.Length.Whole], 0.0f);

            return new NoteInfo(restObj.bounds, restObj.bounds);
        }

        public NoteInfo CreateStaff(ABC.Clef clef, GameObject container, Vector3 offset)
        {
            
            var staff = spriteCache.GetSpriteObject("Staff");
            staff.transform.parent = container.transform;
            staff.transform.localPosition = offset;
            
            Bounds bounds = staff.bounds;
            offset.x += Layout.staffPadding;

            var clefSprite = spriteCache.GetSpriteObject($"Clef_{clef}");
            clefSprite.transform.parent = container.transform;
            clefSprite.transform.localPosition = offset;
            bounds.Encapsulate(clefSprite.bounds);

            return new NoteInfo(bounds, bounds);
        }

        const float TimeSignatureY = 1.15f;

        public NoteInfo CreateTimeSignature(string timeSignature, GameObject container)
        {
            var bounds = new Bounds();

            if (timeSignature == "C" || timeSignature == "C|")
            {
                var spriteName = (timeSignature == "C") ? "Time_Common" : "Time_Cut";
                var commonTime = spriteCache.GetSpriteObject(spriteName);
                commonTime.transform.parent = container.transform;
                commonTime.transform.localPosition = new Vector3(0.0f, TimeSignatureY, 0.0f);
                bounds = commonTime.bounds;
            }
            else
            {
                var pieces = timeSignature.Split('/');
                if (pieces.Length < 2)
                    throw new LayoutException($"Unable to parse time signature: {timeSignature}");

                var sprite = spriteCache.GetSpriteObject($"Time_{pieces[0]}");
                sprite.transform.parent = container.transform;
                sprite.transform.localPosition = new Vector3(0.0f, TimeSignatureY, 0.0f);
                bounds = sprite.bounds;

                sprite = spriteCache.GetSpriteObject($"Time_{pieces[1]}");
                sprite.transform.parent = container.transform;
                sprite.transform.localPosition = Vector3.zero;
                bounds.Encapsulate(sprite.bounds);
            }

            return new NoteInfo(bounds, bounds);
        }

        public NoteInfo CreateBar(ABC.Bar bar, GameObject container)
        {
            var sprite = spriteCache.GetSpriteObject($"Bar_{bar.kind}");
            sprite.transform.parent = container.transform;

            return new NoteInfo(sprite.bounds, sprite.bounds);
        }
    }
}