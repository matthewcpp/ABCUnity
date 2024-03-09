using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using System.Runtime.InteropServices.WindowsRuntime;
using System;
using UnityEditor;

namespace ABCUnity
{
    static class Grouping
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
                startPos += new Vector3(startElement.info.rootBounding.max.x, startElement.info.rootBounding.min.y, 0.0f);
                startAnchor = startPos;

                startPos += new Vector3(0.1f, -0.1f, 0.0f);
                startAnchor.x -= startElement.info.rootBounding.extents.x;

                endPos += endElement.info.rootBounding.min;
                endAnchor = endPos;

                endPos += new Vector3(-0.1f, -0.1f, 0.0f);
                endAnchor.x += endElement.info.rootBounding.extents.x;
            }
            else
            {
                startPos += new Vector3(startElement.info.rootBounding.max.x, startElement.info.rootBounding.max.y, 0.0f);
                startAnchor = startPos;

                startPos += new Vector3(0.1f, 0.1f, 0.0f);
                startAnchor.x -= startElement.info.rootBounding.extents.x;

                endPos += new Vector3(endElement.info.rootBounding.min.x, endElement.info.rootBounding.max.y, 0.0f);
                endAnchor = endPos;

                endPos += new Vector3(-0.1f, 0.1f, 0.0f);
                endAnchor.x += endElement.info.rootBounding.extents.x;
            }

            var boundingY = GetSlurBoundingY(elements, slurPosition);
            Vector3 boundingPt1 = new Vector3(0.0f, boundingY, 0.0f);
            Vector3 boundingPt2 = new Vector3(1.0f, boundingY, 0.0f);

            var lineMidpoint = (startPos + endPos) / 2.0f;
            var anchorMidpoint = (startAnchor + endAnchor) / 2.0f;
            

            var slurMidpoint = MathUtil.LineIntersect(lineMidpoint, anchorMidpoint, boundingPt1, boundingPt2);
            var direction = (lineMidpoint - anchorMidpoint).normalized * ParaoblaMidpointScale;

            var slurLinePoints = CreatePoints(startPos, slurMidpoint + direction, endPos);

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

            matrix = MathUtil.ReducedRowEchelonForm(matrix);
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

        /// Determines the Position of the slur by looking at note directions.
        /// If the stems point up then the slur will be placed below.
        /// If the stems point down then the slur will be placed above.
        /// If the stems are mixed then the slur will be placed above 
        private static SlurPosition DetermineSlurPosition(List<VoiceLayout.ScoreLine.Element> elements)
        {

            var clef = elements[0].measure.scoreLine.voiceLayout.voice.clef;

            // get the initial direction
            int i = 0;
            var initialDirection = NoteCreator.NoteDirection.Unknown;

            for (; i < elements.Count; i++) 
            {
                if (initialDirection != NoteCreator.NoteDirection.Unknown)
                    break;

                initialDirection = NoteCreator.DetermineNoteDirection(elements[i].item, clef);
            }

            for (; i < elements.Count; i++) 
            {
                var direction = NoteCreator.DetermineNoteDirection(elements[i].item, clef);
                if (direction == NoteCreator.NoteDirection.Unknown)
                    continue;

                if (direction != initialDirection)
                    return SlurPosition.Above;
            }

            return initialDirection == NoteCreator.NoteDirection.Up ? SlurPosition.Below : SlurPosition.Above;
        }

        private static float GetSlurBoundingY(List<VoiceLayout.ScoreLine.Element> elements, SlurPosition slurPosition)
        {
            if (slurPosition == SlurPosition.Above)
            {
                float max = float.MinValue;
                foreach(var element in elements)
                {
                    max = Math.Max(max, element.info.rootBounding.max.y);
                }

                return max;
            }
            else
            {
                float min = float.MaxValue;
                foreach(var element in elements)
                {
                    min = Math.Min(min, element.info.rootBounding.min.y);
                }

                return min;
            }
        }
    }
}