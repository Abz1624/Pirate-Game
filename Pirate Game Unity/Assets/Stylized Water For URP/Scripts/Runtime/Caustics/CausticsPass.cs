//━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━																												
// Copyright 2020, Alexander Ameye, All rights reserved.
// https://alexander-ameye.gitbook.io/stylized-water/
//━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━	

#if UNIVERSAL_RENDERER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace StylizedWater
{
    public class CausticsPass : ScriptableRenderPass
    {
        private const string profilerTag = "Caustics Pass";

        public Material causticsMaterial;
        private static Mesh mesh;
        private float waterLevel;

        private const float BIAS = 0.1f;

        class PassData
        {
            public Material material;
            public Mesh mesh;
            public Matrix4x4 matrix;
            public Matrix4x4 sunMatrix;
        }

        public CausticsPass(float waterLevel)
        {
            this.waterLevel = waterLevel;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!causticsMaterial) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var cam = cameraData.camera;
            if (cam.cameraType == CameraType.Preview) return;

            using var builder = renderGraph.AddUnsafePass<PassData>(profilerTag, out var passData);
            var resourceData = frameData.Get<UniversalResourceData>();
            builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
            builder.UseTexture(resourceData.activeDepthTexture, AccessFlags.Read);

            if (!mesh) mesh = GenerateQuad(1000f);

            var position = cam.transform.position;
            position.y = cam.transform.position.y > waterLevel ? waterLevel : cam.transform.position.y - BIAS;

            passData.material = causticsMaterial;
            passData.mesh = mesh;
            passData.matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
            passData.sunMatrix = RenderSettings.sun != null
                ? RenderSettings.sun.transform.localToWorldMatrix
                : Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-45f, 45f, 0f), Vector3.one);

            builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
            {
                if (!data.material || !data.mesh) return;

                data.material.SetMatrix("_MainLightDirection", data.sunMatrix);
                context.cmd.DrawMesh(data.mesh, data.matrix, data.material, 0, 0);
            });
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cam = renderingData.cameraData.camera;

            if (cam.cameraType == CameraType.Preview || !causticsMaterial) return;

            var sunMatrix = RenderSettings.sun != null
                        ? RenderSettings.sun.transform.localToWorldMatrix
                        : Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-45f, 45f, 0f), Vector3.one);
            causticsMaterial.SetMatrix("_MainLightDirection", sunMatrix);

            CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

            if (!mesh) mesh = GenerateQuad(1000f);
            var position = cam.transform.position;
            position.y = cam.transform.position.y > waterLevel ? waterLevel : cam.transform.position.y - BIAS;
            var matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
            cmd.DrawMesh(mesh, matrix, causticsMaterial, 0, 0);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static Mesh GenerateQuad(float size)
        {
            var m = new Mesh();

            size *= 0.5f;

            var verts = new[]
            {
                new Vector3(-size, 0f, -size),
                new Vector3(size, 0f, -size),
                new Vector3(-size, 0f, size),
                new Vector3(size, 0f, size)
            };

            var tris = new[]
            {
                0, 2, 1,
                2, 3, 1
            };

            m.vertices = verts;
            m.triangles = tris;

            return m;
        }
    }
}
#endif