using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ABCUnity
{
    public static class MatrixUtil
    {
        public static float[,] ReducedRowEchelonForm(float[,] matrix)
        {            
            int lead = 0, rowCount = matrix.GetLength(0), columnCount = matrix.GetLength(1);
            for (int r = 0; r < rowCount; r++)
            {
                if (columnCount <= lead) break;
                int i = r;
                while (matrix[i, lead] == 0)
                {
                    i++;
                    if (i == rowCount)
                    {
                        i = r;
                        lead++;
                        if (columnCount == lead)
                        {
                        lead--;
                        break;
                        }
                    }
                }
                for (int j = 0; j < columnCount; j++)
                {
                    float temp = matrix[r, j];
                    matrix[r, j] = matrix[i, j];
                    matrix[i, j] = temp;
                }
                float div = matrix[r, lead];
                if(div != 0)
                    for (int j = 0; j < columnCount; j++) matrix[r, j] /= div;                
                for (int j = 0; j < rowCount; j++)
                {
                    if (j != r)
                    {
                        float sub = matrix[j, lead];
                        for (int k = 0; k < columnCount; k++) matrix[j, k] -= (sub * matrix[r, k]);
                    }
                }
                lead++;
            }
            return matrix;
        }
    }
}