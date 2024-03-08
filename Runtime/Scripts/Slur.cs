using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using System.Runtime.InteropServices.WindowsRuntime;

namespace ABCUnity
{
    static class Slur
    {
        const float ParaoblaMidpointScale = 0.3f;

        private enum SlurPosition {Above, Below};

        public static LineRenderer Create(VoiceLayout.ScoreLine.Element startElement, VoiceLayout.ScoreLine.Element endElement, Material material)
        {
            var elements = CollectElements(startElement, endElement);
            return CreateSingleScorelineSlur(elements, material);
        }

        private static LineRenderer CreateSingleScorelineSlur(List<VoiceLayout.ScoreLine.Element> elements, Material material)
        {
            var startElement = elements[0];
            var endElement = elements[elements.Count - 1];
            var scoreLine = startElement.measure.scoreLine;

            var slurPosition = DetermineSlurPosition(elements);
            var startPos = startElement.container.transform.localPosition + startElement.measure.container.transform.localPosition;
            Vector3 startAnchor, endAnchor;

            var endPos = endElement.container.transform.localPosition + endElement.measure.container.transform.localPosition;

            if (slurPosition == SlurPosition.Below)
            {
                startPos += new Vector3(startElement.info.totalBounding.max.x, startElement.info.totalBounding.min.y, 0.0f);
                startAnchor = startPos;

                startPos += new Vector3(0.1f, -0.1f, 0.0f);
                startAnchor.x -= startElement.info.totalBounding.extents.x;

                endPos += endElement.info.totalBounding.min;
                endAnchor = endPos;

                endPos += new Vector3(-0.1f, -0.1f, 0.0f);
                endAnchor.x += endElement.info.totalBounding.extents.x;
            }
            else
            {
                startPos += new Vector3(startElement.info.totalBounding.max.x, startElement.info.totalBounding.max.y, 0.0f);
                startAnchor = startPos;

                startPos += new Vector3(0.1f, 0.1f, 0.0f);
                startAnchor.x -= startElement.info.totalBounding.extents.x;

                endPos += new Vector3(endElement.info.totalBounding.min.x, endElement.info.totalBounding.max.y, 0.0f);
                endAnchor = endPos;

                endPos += new Vector3(-0.1f, 0.1f, 0.0f);
                endAnchor.x += endElement.info.totalBounding.extents.x;
            }

            var lineMidpoint = (startPos + endPos) / 2.0f;
            var anchorMidpoint = (startAnchor + endAnchor) / 2.0f;
            var direction = (lineMidpoint - anchorMidpoint).normalized * ParaoblaMidpointScale;

            var slurLinePoints = CreatePoints(startPos, lineMidpoint + direction, endPos);

            return CreateLineRenderer(scoreLine, slurLinePoints, material);
        }

        static List<Vector3> CreatePoints(Vector3 startPos, Vector3 midpoint, Vector3 endPos)
        {
            var slurPositions = new List<Vector3>();

            slurPositions.Add(startPos);

            float[,] matrix = new float[3, 4]{
                { startPos.x * startPos.x, startPos.x, 1,  startPos.y },
                { midpoint.x * midpoint.x, midpoint.x, 1,  midpoint.y },
                { endPos.x * endPos.x, endPos.x, 1,  endPos.y  }
            };

            matrix = MatrixUtil.ReducedRowEchelonForm(matrix);
            float a = matrix[0, 3];
            float b = matrix[1, 3];
            float c = matrix[2, 3];

            const int segmentCount = 20;
            float step = (endPos.x - startPos.x) / segmentCount;
            float x = startPos.x;
            for (int i = 0; i < segmentCount; i++) {
                x += step;

                float y = a * (x*x) + b * x + c;

                slurPositions.Add(new Vector3(x, y, 0));
            }

            return slurPositions;
        }

        private static LineRenderer CreateLineRenderer(VoiceLayout.ScoreLine scoreLine, List<Vector3> slurPositions, Material material)
        {
            if (scoreLine.slurs == null) {
                scoreLine.slurs = new GameObject("Slurs");
                scoreLine.slurs.transform.SetParent(scoreLine.container.transform, false);
            }

            var slur = new GameObject("Slur");
            slur.transform.SetParent(scoreLine.slurs.transform, false);

            var lineRenderer = slur.AddComponent<LineRenderer>();
            lineRenderer.positionCount = slurPositions.Count;
            lineRenderer.SetPositions(slurPositions.ToArray());
            
            lineRenderer.useWorldSpace = false;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;

            lineRenderer.material = material;

            return lineRenderer;
        }

        /// <summary>
        /// Returns a list containing all the elements between start and end elements inclusive.
        /// </summary>

        private static List<VoiceLayout.ScoreLine.Element> CollectElements(VoiceLayout.ScoreLine.Element startElement, VoiceLayout.ScoreLine.Element endElement)
        {
            List<VoiceLayout.ScoreLine.Element> elements = new List<VoiceLayout.ScoreLine.Element>();
            var currentElement = startElement;
            var elementIndex = currentElement.measure.elements.FindIndex(e => e == startElement);

            var currentMeasure = currentElement.measure;
            var measureIndex = currentMeasure.scoreLine.measures.FindIndex(m => m == currentMeasure);

            var currentScoreLine = currentMeasure.scoreLine;
            var scoreLineIndex = currentScoreLine.voiceLayout.scoreLines.FindIndex(sl => sl == currentScoreLine);

            var voiceLayout = currentScoreLine.voiceLayout;
            
            while (currentMeasure.elements[elementIndex] != endElement) {
                elements.Add(currentMeasure.elements[elementIndex++]);

                if (elementIndex >= currentMeasure.elements.Count)
                {
                    elementIndex = 0;
                    measureIndex += 1;
                }

                if (measureIndex >= currentScoreLine.measures.Count) {
                    measureIndex = 0;
                    scoreLineIndex += 1;
                }

                currentScoreLine = voiceLayout.scoreLines[scoreLineIndex];
                currentMeasure = currentScoreLine.measures[measureIndex];
                currentElement = currentMeasure.elements[elementIndex];
            }

            elements.Add(endElement);

            return elements;
        }

        private static SlurPosition DetermineSlurPosition(List<VoiceLayout.ScoreLine.Element> elements)
        {
            int total = 0, numItems = 0;
            foreach (var element in elements)
            {
                if (element.item.type == ABC.Item.Type.Note)
                {
                    var note = element.item as ABC.Note;
                    total += (int)note.pitch;
                    numItems += 1;
                }
                else if (element.item.type == ABC.Item.Type.Chord)
                {
                    var chord = element.item as ABC.Chord;
                    int sum = 0;
                    foreach (var chordNote in chord.notes)
                        sum += (int)chordNote.pitch;

                    total += (int)Mathf.Round(sum / (float)chord.notes.Length);
                    numItems += 1;
                }
            }

            float averagePitch = total / (float)numItems;
            return (averagePitch > (float)NoteCreator.clefZero[elements[0].measure.scoreLine.voiceLayout.voice.clef] + 3) ? SlurPosition.Above : SlurPosition.Below;
        }
    }
}