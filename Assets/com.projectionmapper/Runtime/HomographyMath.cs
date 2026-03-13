using UnityEngine;

namespace ProjectionMapper
{
    /// <summary>
    /// Computes a 3x3 homography matrix from 4 source/destination point pairs
    /// using the Direct Linear Transform (DLT) algorithm.
    /// The homography maps normalized screen-space quads to arbitrary 4-corner destinations.
    /// </summary>
    public static class HomographyMath
    {
        /// <summary>
        /// Compute the 3x3 homography matrix that maps the unit square
        /// [(0,0), (1,0), (1,1), (0,1)] to the four destination corners.
        /// Returns the INVERSE homography (dst -> src) for use in the fragment shader,
        /// where we need to look up source UVs from destination screen positions.
        /// </summary>
        /// <param name="dst">Four destination corners in normalized screen space (0-1).
        /// Order: TL, TR, BR, BL (clockwise from top-left).</param>
        /// <returns>A Matrix4x4 with the 3x3 inverse homography packed into it
        /// (row-major in the upper-left 3x3, rest zeroed).</returns>
        public static Matrix4x4 ComputeInverseHomography(Vector2[] dst)
        {
            // Source corners: unit square (the texture UV space)
            Vector2[] src = new Vector2[]
            {
                new Vector2(0f, 1f), // TL
                new Vector2(1f, 1f), // TR
                new Vector2(1f, 0f), // BR
                new Vector2(0f, 0f), // BL
            };

            // Compute forward homography (src -> dst) then invert
            Matrix4x4 H = ComputeHomography(src, dst);
            Matrix4x4 Hinv = Invert3x3(H);
            return Hinv;
        }

        /// <summary>
        /// Compute the forward homography matrix mapping src points to dst points.
        /// Uses the DLT algorithm: builds an 8x9 matrix A from 4 point pairs,
        /// solves Ah=0 via the analytic method for exactly 4 correspondences.
        /// </summary>
        public static Matrix4x4 ComputeHomography(Vector2[] src, Vector2[] dst)
        {
            // For exactly 4 points, we can solve the 8-DOF homography directly.
            // Build the 8x9 matrix A where each correspondence contributes 2 rows.
            // Then solve for the null space of A.

            float[,] A = new float[8, 9];

            for (int i = 0; i < 4; i++)
            {
                float sx = src[i].x;
                float sy = src[i].y;
                float dx = dst[i].x;
                float dy = dst[i].y;

                int r = i * 2;

                // Row 2i: [-sx, -sy, -1, 0, 0, 0, dx*sx, dx*sy, dx]
                A[r, 0] = -sx;
                A[r, 1] = -sy;
                A[r, 2] = -1f;
                A[r, 3] = 0f;
                A[r, 4] = 0f;
                A[r, 5] = 0f;
                A[r, 6] = dx * sx;
                A[r, 7] = dx * sy;
                A[r, 8] = dx;

                // Row 2i+1: [0, 0, 0, -sx, -sy, -1, dy*sx, dy*sy, dy]
                A[r + 1, 0] = 0f;
                A[r + 1, 1] = 0f;
                A[r + 1, 2] = 0f;
                A[r + 1, 3] = -sx;
                A[r + 1, 4] = -sy;
                A[r + 1, 5] = -1f;
                A[r + 1, 6] = dy * sx;
                A[r + 1, 7] = dy * sy;
                A[r + 1, 8] = dy;
            }

            // Solve via SVD-like approach: compute A^T * A, find its null space.
            // For a 9x9 symmetric matrix with rank 8, the null vector is the
            // eigenvector corresponding to the smallest eigenvalue.
            // We use a simplified approach: Gaussian elimination on the 8x9 system.
            float[] h = SolveNullSpace8x9(A);

            Matrix4x4 H = Matrix4x4.identity;
            H.m00 = h[0]; H.m01 = h[1]; H.m02 = h[2];
            H.m10 = h[3]; H.m11 = h[4]; H.m12 = h[5];
            H.m20 = h[6]; H.m21 = h[7]; H.m22 = h[8];
            H.m03 = 0f; H.m13 = 0f; H.m23 = 0f;
            H.m30 = 0f; H.m31 = 0f; H.m32 = 0f; H.m33 = 1f;

            return H;
        }

        /// <summary>
        /// Solve for the null space of an 8x9 matrix using Gaussian elimination
        /// with partial pivoting. Returns the 9-element null vector.
        /// </summary>
        private static float[] SolveNullSpace8x9(float[,] A)
        {
            // Copy to working matrix
            float[,] M = new float[8, 9];
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 9; j++)
                    M[i, j] = A[i, j];

            // Forward elimination with partial pivoting
            for (int col = 0; col < 8; col++)
            {
                // Find pivot
                int maxRow = col;
                float maxVal = Mathf.Abs(M[col, col]);
                for (int row = col + 1; row < 8; row++)
                {
                    float val = Mathf.Abs(M[row, col]);
                    if (val > maxVal)
                    {
                        maxVal = val;
                        maxRow = row;
                    }
                }

                // Swap rows
                if (maxRow != col)
                {
                    for (int j = 0; j < 9; j++)
                    {
                        float tmp = M[col, j];
                        M[col, j] = M[maxRow, j];
                        M[maxRow, j] = tmp;
                    }
                }

                // Eliminate below
                float pivot = M[col, col];
                if (Mathf.Abs(pivot) < 1e-10f) continue;

                for (int row = col + 1; row < 8; row++)
                {
                    float factor = M[row, col] / pivot;
                    for (int j = col; j < 9; j++)
                    {
                        M[row, j] -= factor * M[col, j];
                    }
                }
            }

            // Back substitution: express variables 0-7 in terms of variable 8
            // Set h[8] = 1 (homography is defined up to scale)
            float[] h = new float[9];
            h[8] = 1f;

            for (int i = 7; i >= 0; i--)
            {
                float sum = M[i, 8] * h[8];
                for (int j = i + 1; j < 8; j++)
                {
                    sum += M[i, j] * h[j];
                }
                float diag = M[i, i];
                if (Mathf.Abs(diag) < 1e-10f)
                {
                    h[i] = 0f;
                }
                else
                {
                    h[i] = -sum / diag;
                }
            }

            // Normalize so that the largest element is 1 for numerical stability
            float maxAbs = 0f;
            for (int i = 0; i < 9; i++)
            {
                float absVal = Mathf.Abs(h[i]);
                if (absVal > maxAbs) maxAbs = absVal;
            }
            if (maxAbs > 1e-10f)
            {
                for (int i = 0; i < 9; i++)
                    h[i] /= maxAbs;
            }

            return h;
        }

        /// <summary>
        /// Invert a 3x3 matrix stored in the upper-left of a Matrix4x4.
        /// </summary>
        public static Matrix4x4 Invert3x3(Matrix4x4 M)
        {
            float a = M.m00, b = M.m01, c = M.m02;
            float d = M.m10, e = M.m11, f = M.m12;
            float g = M.m20, h = M.m21, i = M.m22;

            float det = a * (e * i - f * h)
                      - b * (d * i - f * g)
                      + c * (d * h - e * g);

            if (Mathf.Abs(det) < 1e-10f)
            {
                Debug.LogWarning("HomographyMath: Singular matrix, returning identity.");
                return Matrix4x4.identity;
            }

            float invDet = 1f / det;

            Matrix4x4 inv = Matrix4x4.identity;
            inv.m00 = (e * i - f * h) * invDet;
            inv.m01 = (c * h - b * i) * invDet;
            inv.m02 = (b * f - c * e) * invDet;
            inv.m10 = (f * g - d * i) * invDet;
            inv.m11 = (a * i - c * g) * invDet;
            inv.m12 = (c * d - a * f) * invDet;
            inv.m20 = (d * h - e * g) * invDet;
            inv.m21 = (b * g - a * h) * invDet;
            inv.m22 = (a * e - b * d) * invDet;
            inv.m03 = 0f; inv.m13 = 0f; inv.m23 = 0f;
            inv.m30 = 0f; inv.m31 = 0f; inv.m32 = 0f;
            inv.m33 = 1f;

            return inv;
        }

        /// <summary>
        /// Transform a 2D point through a 3x3 homography stored in a Matrix4x4.
        /// Performs the perspective divide.
        /// </summary>
        public static Vector2 TransformPoint(Matrix4x4 H, Vector2 p)
        {
            float x = H.m00 * p.x + H.m01 * p.y + H.m02;
            float y = H.m10 * p.x + H.m11 * p.y + H.m12;
            float w = H.m20 * p.x + H.m21 * p.y + H.m22;

            if (Mathf.Abs(w) < 1e-10f) return p;
            return new Vector2(x / w, y / w);
        }
    }
}
