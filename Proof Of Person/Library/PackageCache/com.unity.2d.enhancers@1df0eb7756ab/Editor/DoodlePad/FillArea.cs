using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

namespace Unity.U2D.AI.Editor
{
    internal static class FillArea
    {
        class FillAreaComputeShader
        {
            ComputeShader m_ComputeShader = null;

            static readonly int k_ResultTexture = Shader.PropertyToID("ResultTexture");
            static readonly int k_Poly = Shader.PropertyToID("Poly");
            static readonly int k_PolyCount = Shader.PropertyToID("PolyCount");
            static readonly int k_Width = Shader.PropertyToID("Width");
            static readonly int k_PaintColor = Shader.PropertyToID("PaintColor");

            readonly int k_Kernel;
            const string k_KernelName = "PaintAreaCS";

            public ComputeShader shader
            {
                get
                {
                    if (m_ComputeShader == null)
                    {
                        m_ComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Packages/com.unity.2d.enhancers/Editor/PackageResources/DoodlePad/PaintArea.compute");
                    }

                    return m_ComputeShader;
                }
            }

            public FillAreaComputeShader()
            {
                k_Kernel = shader.FindKernel(k_KernelName);
            }

            public void Init(Texture texture, ComputeBuffer polyBuffer, Vector2Int size, Color color)
            {
                shader.SetTexture(k_Kernel, k_ResultTexture, texture);
                shader.SetBuffer(k_Kernel, k_Poly, polyBuffer);
                shader.SetInt(k_PolyCount, polyBuffer.count);
                shader.SetVector(k_PaintColor, new Vector4(color.r, color.g, color.b, color.a));
                shader.SetInt(k_Width, size.x);
            }

            public void Dispatch(int threadGroupsX, int threadGroupsY, int threadGroupsZ)
            {
                shader.Dispatch(k_Kernel, threadGroupsX, threadGroupsY, threadGroupsZ);
            }
        }

        public static bool disableComputeShader = false;
        public static bool useComputeShader => !disableComputeShader && SystemInfo.supportsComputeShaders;

        static FillAreaComputeShader computeShader => s_ComputeShader ??= new FillAreaComputeShader();
        static FillAreaComputeShader s_ComputeShader;

        public static void FillTextureCompute(RenderTexture renderTexture, Vector2[] area, Vector2Int size, Color color)
        {
            var polyCount = area.Length;
            var poly = new NativeArray<Vector2>(area, Allocator.TempJob);

            var polyBuffer = new ComputeBuffer(polyCount, sizeof(float) * 2);
            polyBuffer.SetData(poly);

            computeShader.Init(renderTexture, polyBuffer, size, color);

            var threadGroupsX = Mathf.CeilToInt(size.x / 8f);
            var threadGroupsY = Mathf.CeilToInt(size.y / 8f);
            var threadGroupsZ = 1;
            computeShader.Dispatch(threadGroupsX, threadGroupsY, threadGroupsZ);

            polyBuffer.Release();
            poly.Dispose();
        }

        public static void FillTextureJob(Texture2D texture, Vector2[] area, Vector2Int size, Color color)
        {
            var doodleCanvas = texture.GetPixels();
            var pixelArray = new NativeArray<Color>(doodleCanvas, Allocator.TempJob);
            var poly = new NativeArray<Vector2>(area, Allocator.TempJob);
            var job = new PaintAreaJob
            {
                pixels = pixelArray,
                poly = poly,
                width = size.x,
                color = color
            };

            var handle = job.Schedule(pixelArray.Length, 64);
            handle.Complete();

            pixelArray.CopyTo(doodleCanvas);
            pixelArray.Dispose();

            poly.Dispose();

            texture.SetPixels(doodleCanvas);
            texture.Apply();
        }

        [BurstCompile]
        struct PaintAreaJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Vector2> poly;
            public NativeArray<Color> pixels;

            public int width;
            public Color color;

            public void Execute(int index)
            {
                var point = new Vector2(index % width, index / width);

                var polyCount = poly.Length;
                var windingNumber = 0;

                for (var i = 0; i < polyCount; i++)
                {
                    var v1 = poly[i];
                    var v2 = poly[(i + 1) % polyCount];

                    var upwardCrossing = v1.y <= point.y && v2.y > point.y;
                    var downwardCrossing = v1.y > point.y && v2.y <= point.y;

                    var isLeft = (v2.x - v1.x) * (point.y - v1.y) - (point.x - v1.x) * (v2.y - v1.y);

                    if (upwardCrossing && isLeft > 0)
                        windingNumber++; // Counter-clockwise turn
                    else if (downwardCrossing && isLeft < 0)
                        windingNumber--; // Clockwise turn
                }

                if (windingNumber != 0)
                    pixels[index] = color;
            }
        }
    }
}