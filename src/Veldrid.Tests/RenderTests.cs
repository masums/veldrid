﻿using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace Veldrid.Tests
{
    internal struct UIntVertexAttribsVertex
    {
        public Vector2 Position;
        public UInt4 Color_Int;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UIntVertexAttribsInfo
    {
        public uint ColorNormalizationFactor;
        private float padding0;
        private float padding1;
        private float padding2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ColoredVertex
    {
        public Vector4 Color;
        public Vector2 Position;
        private Vector2 _padding0;
    }

    public abstract class RenderTests<T> : GraphicsDeviceTestBase<T> where T : GraphicsDeviceCreator
    {
        [Fact]
        public void Points_WithUIntColor()
        {
            Texture target = RF.CreateTexture(TextureDescription.Texture2D(
                50, 50, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.RenderTarget));
            Texture staging = RF.CreateTexture(TextureDescription.Texture2D(
                50, 50, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Staging));

            Framebuffer framebuffer = RF.CreateFramebuffer(new FramebufferDescription(null, target));

            DeviceBuffer infoBuffer = RF.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
            DeviceBuffer orthoBuffer = RF.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            Matrix4x4 orthoMatrix = Matrix4x4.CreateOrthographicOffCenter(
                0,
                framebuffer.Width,
                framebuffer.Height,
                0,
                -1,
                1);
            GD.UpdateBuffer(orthoBuffer, 0, ref orthoMatrix);

            ShaderSetDescription shaderSet = new ShaderSetDescription(
                new VertexLayoutDescription[]
                {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                        new VertexElementDescription("Color_UInt", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt4))
                },
                TestShaders.LoadVertexFragment(RF, "UIntVertexAttribs"));

            ResourceLayout layout = RF.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("InfoBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("Ortho", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceSet set = RF.CreateResourceSet(new ResourceSetDescription(layout, infoBuffer, orthoBuffer));

            GraphicsPipelineDescription gpd = new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.Default,
                PrimitiveTopology.PointList,
                shaderSet,
                layout,
                framebuffer.OutputDescription);

            Pipeline pipeline = RF.CreateGraphicsPipeline(ref gpd);

            uint colorNormalizationFactor = 2500;

            UIntVertexAttribsVertex[] vertices = new UIntVertexAttribsVertex[]
            {
                new UIntVertexAttribsVertex
                {
                    Position = new Vector2(0.5f, 0.5f),
                    Color_Int = new UInt4
                    {
                        X = (uint)(0.25f * colorNormalizationFactor),
                        Y = (uint)(0.5f * colorNormalizationFactor),
                        Z = (uint)(0.75f * colorNormalizationFactor),
                    }
                },
                new UIntVertexAttribsVertex
                {
                    Position = new Vector2(10.5f, 12.5f),
                    Color_Int = new UInt4
                    {
                        X = (uint)(0.25f * colorNormalizationFactor),
                        Y = (uint)(0.5f * colorNormalizationFactor),
                        Z = (uint)(0.75f * colorNormalizationFactor),
                    }
                },
                new UIntVertexAttribsVertex
                {
                    Position = new Vector2(25.5f, 35.5f),
                    Color_Int = new UInt4
                    {
                        X = (uint)(0.75f * colorNormalizationFactor),
                        Y = (uint)(0.5f * colorNormalizationFactor),
                        Z = (uint)(0.25f * colorNormalizationFactor),
                    }
                },
                new UIntVertexAttribsVertex
                {
                    Position = new Vector2(49.5f, 49.5f),
                    Color_Int = new UInt4
                    {
                        X = (uint)(0.15f * colorNormalizationFactor),
                        Y = (uint)(0.25f * colorNormalizationFactor),
                        Z = (uint)(0.35f * colorNormalizationFactor),
                    }
                },
            };

            DeviceBuffer vb = RF.CreateBuffer(
                new BufferDescription((uint)(Unsafe.SizeOf<UIntVertexAttribsVertex>() * vertices.Length), BufferUsage.VertexBuffer));
            GD.UpdateBuffer(vb, 0, vertices);
            GD.UpdateBuffer(infoBuffer, 0, new UIntVertexAttribsInfo { ColorNormalizationFactor = colorNormalizationFactor });

            CommandList cl = RF.CreateCommandList();

            cl.Begin();
            cl.SetFramebuffer(framebuffer);
            cl.SetFullViewports();
            cl.SetFullScissorRects();
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.SetPipeline(pipeline);
            cl.SetVertexBuffer(0, vb);
            cl.SetGraphicsResourceSet(0, set);
            cl.Draw((uint)vertices.Length);
            cl.CopyTexture(target, staging);
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();

            MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(staging, MapMode.Read);

            foreach (UIntVertexAttribsVertex vertex in vertices)
            {
                uint x = (uint)vertex.Position.X;
                uint y = (uint)vertex.Position.Y;
                if (!GD.IsUvOriginTopLeft || GD.IsClipSpaceYInverted)
                {
                    y = framebuffer.Height - y - 1;
                }

                RgbaFloat expectedColor = new RgbaFloat(
                    vertex.Color_Int.X / (float)colorNormalizationFactor,
                    vertex.Color_Int.Y / (float)colorNormalizationFactor,
                    vertex.Color_Int.Z / (float)colorNormalizationFactor,
                    1);
                Assert.Equal(expectedColor, readView[x, y], RgbaFloatFuzzyComparer.Instance);
            }
            GD.Unmap(staging);
        }

        [Fact]
        public void Points_WithUShortNormColor()
        {
            Texture target = RF.CreateTexture(TextureDescription.Texture2D(
                50, 50, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.RenderTarget));
            Texture staging = RF.CreateTexture(TextureDescription.Texture2D(
                50, 50, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Staging));

            Framebuffer framebuffer = RF.CreateFramebuffer(new FramebufferDescription(null, target));

            DeviceBuffer orthoBuffer = RF.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            Matrix4x4 orthoMatrix = Matrix4x4.CreateOrthographicOffCenter(
                0,
                framebuffer.Width,
                framebuffer.Height,
                0,
                -1,
                1);
            GD.UpdateBuffer(orthoBuffer, 0, ref orthoMatrix);

            ShaderSetDescription shaderSet = new ShaderSetDescription(
                new VertexLayoutDescription[]
                {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                        new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UShort4_Norm))
                },
                TestShaders.LoadVertexFragment(RF, "U16NormVertexAttribs"));

            ResourceLayout layout = RF.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Ortho", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceSet set = RF.CreateResourceSet(new ResourceSetDescription(layout, orthoBuffer));

            GraphicsPipelineDescription gpd = new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.Default,
                PrimitiveTopology.PointList,
                shaderSet,
                layout,
                framebuffer.OutputDescription);

            Pipeline pipeline = RF.CreateGraphicsPipeline(ref gpd);

            VertexCPU_UShortNorm[] vertices = new VertexCPU_UShortNorm[]
            {
                new VertexCPU_UShortNorm
                {
                    Position = new Vector2(0.5f, 0.5f),
                    R = UShortNorm(0.25f),
                    G = UShortNorm(0.5f),
                    B = UShortNorm(0.75f),
                },
                new VertexCPU_UShortNorm
                {
                    Position = new Vector2(10.5f, 12.5f),
                    R = UShortNorm(0.25f),
                    G = UShortNorm(0.5f),
                    B = UShortNorm(0.75f),
                },
                new VertexCPU_UShortNorm
                {
                    Position = new Vector2(25.5f, 35.5f),
                    R = UShortNorm(0.75f),
                    G = UShortNorm(0.5f),
                    B = UShortNorm(0.25f),
                },
                new VertexCPU_UShortNorm
                {
                    Position = new Vector2(49.5f, 49.5f),
                    R = UShortNorm(0.15f),
                    G = UShortNorm(0.25f),
                    B = UShortNorm(0.35f),
                },
            };

            DeviceBuffer vb = RF.CreateBuffer(
                new BufferDescription((uint)(Unsafe.SizeOf<VertexCPU_UShortNorm>() * vertices.Length), BufferUsage.VertexBuffer));
            GD.UpdateBuffer(vb, 0, vertices);

            CommandList cl = RF.CreateCommandList();

            cl.Begin();
            cl.SetFramebuffer(framebuffer);
            cl.SetFullViewports();
            cl.SetFullScissorRects();
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.SetPipeline(pipeline);
            cl.SetVertexBuffer(0, vb);
            cl.SetGraphicsResourceSet(0, set);
            cl.Draw((uint)vertices.Length);
            cl.CopyTexture(target, staging);
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();

            MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(staging, MapMode.Read);

            foreach (VertexCPU_UShortNorm vertex in vertices)
            {
                uint x = (uint)vertex.Position.X;
                uint y = (uint)vertex.Position.Y;
                if (!GD.IsUvOriginTopLeft || GD.IsClipSpaceYInverted)
                {
                    y = framebuffer.Height - y - 1;
                }

                RgbaFloat expectedColor = new RgbaFloat(
                    vertex.R / (float)ushort.MaxValue,
                    vertex.G / (float)ushort.MaxValue,
                    vertex.B / (float)ushort.MaxValue,
                    1);
                Assert.Equal(expectedColor, readView[x, y], RgbaFloatFuzzyComparer.Instance);
            }
            GD.Unmap(staging);
        }

        public struct VertexCPU_UShortNorm
        {
            public Vector2 Position;
            public ushort R;
            public ushort G;
            public ushort B;
            public ushort A;
        }

        public struct VertexCPU_UShort
        {
            public Vector2 Position;
            public ushort R;
            public ushort G;
            public ushort B;
            public ushort A;
        }

        private ushort UShortNorm(float normalizedValue)
        {
            Debug.Assert(normalizedValue >= 0 && normalizedValue <= 1);
            return (ushort)(normalizedValue * ushort.MaxValue);
        }

        [Fact]
        public void Points_WithUShortColor()
        {
            Texture target = RF.CreateTexture(TextureDescription.Texture2D(
                50, 50, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.RenderTarget));
            Texture staging = RF.CreateTexture(TextureDescription.Texture2D(
                50, 50, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Staging));

            Framebuffer framebuffer = RF.CreateFramebuffer(new FramebufferDescription(null, target));

            DeviceBuffer infoBuffer = RF.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
            DeviceBuffer orthoBuffer = RF.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            Matrix4x4 orthoMatrix = Matrix4x4.CreateOrthographicOffCenter(
                0,
                framebuffer.Width,
                framebuffer.Height,
                0,
                -1,
                1);
            GD.UpdateBuffer(orthoBuffer, 0, ref orthoMatrix);

            ShaderSetDescription shaderSet = new ShaderSetDescription(
                new VertexLayoutDescription[]
                {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                        new VertexElementDescription("Color_UInt", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UShort4))
                },
                TestShaders.LoadVertexFragment(RF, "U16VertexAttribs"));

            ResourceLayout layout = RF.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("InfoBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("Ortho", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceSet set = RF.CreateResourceSet(new ResourceSetDescription(layout, infoBuffer, orthoBuffer));

            GraphicsPipelineDescription gpd = new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.Default,
                PrimitiveTopology.PointList,
                shaderSet,
                layout,
                framebuffer.OutputDescription);

            Pipeline pipeline = RF.CreateGraphicsPipeline(ref gpd);

            uint colorNormalizationFactor = 2500;

            VertexCPU_UShort[] vertices = new VertexCPU_UShort[]
            {
                new VertexCPU_UShort
                {
                    Position = new Vector2(0.5f, 0.5f),
                    R = (ushort)(0.25f * colorNormalizationFactor),
                    G = (ushort)(0.5f * colorNormalizationFactor),
                    B = (ushort)(0.75f * colorNormalizationFactor),
                },
                new VertexCPU_UShort
                {
                    Position = new Vector2(10.5f, 12.5f),
                    R = (ushort)(0.25f * colorNormalizationFactor),
                    G = (ushort)(0.5f * colorNormalizationFactor),
                    B = (ushort)(0.75f * colorNormalizationFactor),
                },
                new VertexCPU_UShort
                {
                    Position = new Vector2(25.5f, 35.5f),
                    R = (ushort)(0.75f * colorNormalizationFactor),
                    G = (ushort)(0.5f * colorNormalizationFactor),
                    B = (ushort)(0.25f * colorNormalizationFactor),
                },
                new VertexCPU_UShort
                {
                    Position = new Vector2(49.5f, 49.5f),
                    R = (ushort)(0.15f * colorNormalizationFactor),
                    G = (ushort)(0.2f * colorNormalizationFactor),
                    B = (ushort)(0.35f * colorNormalizationFactor),
                },
            };

            DeviceBuffer vb = RF.CreateBuffer(
                new BufferDescription((uint)(Unsafe.SizeOf<UIntVertexAttribsVertex>() * vertices.Length), BufferUsage.VertexBuffer));
            GD.UpdateBuffer(vb, 0, vertices);
            GD.UpdateBuffer(infoBuffer, 0, new UIntVertexAttribsInfo { ColorNormalizationFactor = colorNormalizationFactor });

            CommandList cl = RF.CreateCommandList();

            cl.Begin();
            cl.SetFramebuffer(framebuffer);
            cl.SetFullViewports();
            cl.SetFullScissorRects();
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.SetPipeline(pipeline);
            cl.SetVertexBuffer(0, vb);
            cl.SetGraphicsResourceSet(0, set);
            cl.Draw((uint)vertices.Length);
            cl.CopyTexture(target, staging);
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();

            MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(staging, MapMode.Read);

            foreach (VertexCPU_UShort vertex in vertices)
            {
                uint x = (uint)vertex.Position.X;
                uint y = (uint)vertex.Position.Y;
                if (!GD.IsUvOriginTopLeft || GD.IsClipSpaceYInverted)
                {
                    y = framebuffer.Height - y - 1;
                }

                RgbaFloat expectedColor = new RgbaFloat(
                    vertex.R / (float)colorNormalizationFactor,
                    vertex.G / (float)colorNormalizationFactor,
                    vertex.B / (float)colorNormalizationFactor,
                    1);
                Assert.Equal(expectedColor, readView[x, y], RgbaFloatFuzzyComparer.Instance);
            }
            GD.Unmap(staging);
        }

        [Fact]
        public void Points_WithFloat16Color()
        {
            Texture target = RF.CreateTexture(TextureDescription.Texture2D(
                50, 50, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.RenderTarget));
            Texture staging = RF.CreateTexture(TextureDescription.Texture2D(
                50, 50, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Staging));

            Framebuffer framebuffer = RF.CreateFramebuffer(new FramebufferDescription(null, target));

            DeviceBuffer infoBuffer = RF.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
            DeviceBuffer orthoBuffer = RF.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            Matrix4x4 orthoMatrix = Matrix4x4.CreateOrthographicOffCenter(
                0,
                framebuffer.Width,
                framebuffer.Height,
                0,
                -1,
                1);
            GD.UpdateBuffer(orthoBuffer, 0, ref orthoMatrix);

            ShaderSetDescription shaderSet = new ShaderSetDescription(
                new VertexLayoutDescription[]
                {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                        new VertexElementDescription("Color_Half", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Half4))
                },
                TestShaders.LoadVertexFragment(RF, "F16VertexAttribs"));

            ResourceLayout layout = RF.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("InfoBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("OrthoBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceSet set = RF.CreateResourceSet(new ResourceSetDescription(layout, infoBuffer, orthoBuffer));

            GraphicsPipelineDescription gpd = new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.Default,
                PrimitiveTopology.PointList,
                shaderSet,
                layout,
                framebuffer.OutputDescription);

            Pipeline pipeline = RF.CreateGraphicsPipeline(ref gpd);

            uint colorNormalizationFactor = 2500;

            const ushort f16_375 = 0x5DDC; // 375.0
            const ushort f16_500 = 0x5FD0; // 500.0
            const ushort f16_625 = 0x60E2; // 625.0
            const ushort f16_875 = 0x62D6; // 875.0
            const ushort f16_1250 = 0x64E2; // 1250.0
            const ushort f16_1875 = 0x6753; // 1875.0

            VertexCPU_UShort[] vertices = new VertexCPU_UShort[]
            {
                new VertexCPU_UShort
                {
                    Position = new Vector2(0.5f, 0.5f),
                    R = f16_625,
                    G = f16_1250,
                    B = f16_1875,
                },
                new VertexCPU_UShort
                {
                    Position = new Vector2(10.5f, 12.5f),
                    R = f16_625,
                    G = f16_1250,
                    B = f16_1875,
                },
                new VertexCPU_UShort
                {
                    Position = new Vector2(25.5f, 35.5f),
                    R = f16_1875,
                    G = f16_1250,
                    B = f16_625,
                },
                new VertexCPU_UShort
                {
                    Position = new Vector2(49.5f, 49.5f),
                    R = f16_375,
                    G = f16_500,
                    B = f16_875,
                },
            };

            RgbaFloat[] expectedColors = new[]
            {
                new RgbaFloat(
                    625.0f / colorNormalizationFactor,
                    1250.0f / colorNormalizationFactor,
                    1875.0f / colorNormalizationFactor,
                    1),
                new RgbaFloat(
                    625.0f / colorNormalizationFactor,
                    1250.0f / colorNormalizationFactor,
                    1875.0f / colorNormalizationFactor,
                    1),
                new RgbaFloat(
                    1875.0f / colorNormalizationFactor,
                    1250.0f / colorNormalizationFactor,
                    625.0f / colorNormalizationFactor,
                    1),
                new RgbaFloat(
                    375.0f / colorNormalizationFactor,
                    500.0f / colorNormalizationFactor,
                    875.0f / colorNormalizationFactor,
                    1),
            };

            DeviceBuffer vb = RF.CreateBuffer(
                new BufferDescription((uint)(Unsafe.SizeOf<UIntVertexAttribsVertex>() * vertices.Length), BufferUsage.VertexBuffer));
            GD.UpdateBuffer(vb, 0, vertices);
            GD.UpdateBuffer(infoBuffer, 0, new UIntVertexAttribsInfo { ColorNormalizationFactor = colorNormalizationFactor });

            CommandList cl = RF.CreateCommandList();

            cl.Begin();
            cl.SetFramebuffer(framebuffer);
            cl.SetFullViewports();
            cl.SetFullScissorRects();
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.SetPipeline(pipeline);
            cl.SetVertexBuffer(0, vb);
            cl.SetGraphicsResourceSet(0, set);
            cl.Draw((uint)vertices.Length);
            cl.CopyTexture(target, staging);
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();

            MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(staging, MapMode.Read);

            for (int i = 0; i < vertices.Length; i++)
            {
                VertexCPU_UShort vertex = vertices[i];
                uint x = (uint)vertex.Position.X;
                uint y = (uint)vertex.Position.Y;
                if (!GD.IsUvOriginTopLeft || GD.IsClipSpaceYInverted)
                {
                    y = framebuffer.Height - y - 1;
                }

                RgbaFloat expectedColor = expectedColors[i];
                Assert.Equal(expectedColor, readView[x, y], RgbaFloatFuzzyComparer.Instance);
            }
            GD.Unmap(staging);
        }

        [InlineData(false)]
        [InlineData(true)]
        [Theory]
        public unsafe void Points_WithTexture_UpdateUnrelated(bool useTextureView)
        {
            // This is a regression test for the case where a user modifies an unrelated texture
            // at a time after a ResourceSet containing a texture has been bound. The OpenGL
            // backend was caching texture state improperly, resulting in wrong textures being sampled.

            Texture target = RF.CreateTexture(TextureDescription.Texture2D(
                50, 50, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.RenderTarget));
            Texture staging = RF.CreateTexture(TextureDescription.Texture2D(
                50, 50, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Staging));

            Framebuffer framebuffer = RF.CreateFramebuffer(new FramebufferDescription(null, target));

            DeviceBuffer orthoBuffer = RF.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            Matrix4x4 orthoMatrix = Matrix4x4.CreateOrthographicOffCenter(
                0,
                framebuffer.Width,
                framebuffer.Height,
                0,
                -1,
                1);
            GD.UpdateBuffer(orthoBuffer, 0, ref orthoMatrix);

            Texture sampledTexture = RF.CreateTexture(
                TextureDescription.Texture2D(1, 1, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Sampled));

            RgbaFloat white = RgbaFloat.White;
            GD.UpdateTexture(sampledTexture, (IntPtr)(&white), (uint)Unsafe.SizeOf<RgbaFloat>(), 0, 0, 0, 1, 1, 1, 0, 0);

            Texture shouldntBeSampledTexture = RF.CreateTexture(
                TextureDescription.Texture2D(1, 1, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Sampled));

            ShaderSetDescription shaderSet = new ShaderSetDescription(
                new VertexLayoutDescription[]
                {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
                },
                TestShaders.LoadVertexFragment(RF, "TexturedPoints"));

            ResourceLayout layout = RF.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Ortho", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("Tex", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("Smp", ResourceKind.Sampler, ShaderStages.Fragment)));

            ResourceSet set;
            if (useTextureView)
            {
                TextureView view = RF.CreateTextureView(sampledTexture);
                set = RF.CreateResourceSet(new ResourceSetDescription(layout, orthoBuffer, view, GD.PointSampler));
            }
            else
            {
                set = RF.CreateResourceSet(new ResourceSetDescription(layout, orthoBuffer, sampledTexture, GD.PointSampler));
            }

            GraphicsPipelineDescription gpd = new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.Default,
                PrimitiveTopology.PointList,
                shaderSet,
                layout,
                framebuffer.OutputDescription);

            Pipeline pipeline = RF.CreateGraphicsPipeline(ref gpd);

            Vector2[] vertices = new Vector2[]
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(15.5f, 15.5f),
                new Vector2(25.5f, 26.5f),
                new Vector2(3.5f, 25.5f),
            };

            DeviceBuffer vb = RF.CreateBuffer(
                new BufferDescription((uint)(Unsafe.SizeOf<Vector2>() * vertices.Length), BufferUsage.VertexBuffer));
            GD.UpdateBuffer(vb, 0, vertices);

            CommandList cl = RF.CreateCommandList();

            for (int i = 0; i < 2; i++)
            {
                cl.Begin();
                cl.SetFramebuffer(framebuffer);
                cl.ClearColorTarget(0, RgbaFloat.Black);
                cl.SetPipeline(pipeline);
                cl.SetVertexBuffer(0, vb);
                cl.SetGraphicsResourceSet(0, set);

                // Modify an unrelated texture.
                // This must have no observable effect on the next draw call.
                RgbaFloat pink = RgbaFloat.Pink;
                GD.UpdateTexture(shouldntBeSampledTexture,
                    (IntPtr)(&pink), (uint)Unsafe.SizeOf<RgbaFloat>(),
                    0, 0, 0,
                    1, 1, 1,
                    0, 0);

                cl.Draw((uint)vertices.Length);
                cl.End();
                GD.SubmitCommands(cl);
                GD.WaitForIdle();
            }

            cl.Begin();
            cl.CopyTexture(target, staging);
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();

            MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(staging, MapMode.Read);

            foreach (Vector2 vertex in vertices)
            {
                uint x = (uint)vertex.X;
                uint y = (uint)vertex.Y;
                if (!GD.IsUvOriginTopLeft || GD.IsClipSpaceYInverted)
                {
                    y = framebuffer.Height - y - 1;
                }

                Assert.Equal(white, readView[x, y], RgbaFloatFuzzyComparer.Instance);
            }
            GD.Unmap(staging);
        }

        [Fact]
        public void ComputeGeneratedVertices()
        {
            if (!GD.Features.ComputeShader)
            {
                return;
            }

            uint width = 512;
            uint height = 512;
            Texture output = RF.CreateTexture(
                TextureDescription.Texture2D(width, height, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.RenderTarget));
            Framebuffer framebuffer = RF.CreateFramebuffer(new FramebufferDescription(null, output));

            uint vertexSize = (uint)Unsafe.SizeOf<ColoredVertex>();
            DeviceBuffer buffer = RF.CreateBuffer(new BufferDescription(
                vertexSize * 4,
                BufferUsage.StructuredBufferReadWrite,
                vertexSize,
                true));

            ResourceLayout computeLayout = RF.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("OutputVertices", ResourceKind.StructuredBufferReadWrite, ShaderStages.Compute)));
            ResourceSet computeSet = RF.CreateResourceSet(new ResourceSetDescription(computeLayout, buffer));

            Pipeline computePipeline = RF.CreateComputePipeline(new ComputePipelineDescription(
                TestShaders.LoadCompute(RF, "ComputeColoredQuadGenerator"),
                computeLayout,
                1, 1, 1));

            ResourceLayout graphicsLayout = RF.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("InputVertices", ResourceKind.StructuredBufferReadOnly, ShaderStages.Vertex)));
            ResourceSet graphicsSet = RF.CreateResourceSet(new ResourceSetDescription(graphicsLayout, buffer));

            Pipeline graphicsPipeline = RF.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.Default,
                PrimitiveTopology.TriangleStrip,
                new ShaderSetDescription(
                    Array.Empty<VertexLayoutDescription>(),
                    TestShaders.LoadVertexFragment(RF, "ColoredQuadRenderer")),
                graphicsLayout,
                framebuffer.OutputDescription));

            CommandList cl = RF.CreateCommandList();
            cl.Begin();
            cl.SetPipeline(computePipeline);
            cl.SetComputeResourceSet(0, computeSet);
            cl.Dispatch(1, 1, 1);
            cl.SetFramebuffer(framebuffer);
            cl.ClearColorTarget(0, new RgbaFloat());
            cl.SetPipeline(graphicsPipeline);
            cl.SetGraphicsResourceSet(0, graphicsSet);
            cl.Draw(4);
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();

            Texture readback = GetReadback(output);
            MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(readback, MapMode.Read);
            for (uint y = 0; y < height; y++)
                for (uint x = 0; x < width; x++)
                {
                    Assert.Equal(RgbaFloat.Red, readView[x, y]);
                }
            GD.Unmap(readback);
        }

        [Fact]
        public void ComputeGeneratedTexture()
        {
            if (!GD.Features.ComputeShader)
            {
                return;
            }

            uint width = 4;
            uint height = 1;
            TextureDescription texDesc = TextureDescription.Texture2D(
                width, height,
                1,
                1,
                PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.Sampled | TextureUsage.Storage);
            Texture computeOutput = RF.CreateTexture(texDesc);
            texDesc.Usage = TextureUsage.RenderTarget;
            Texture finalOutput = RF.CreateTexture(texDesc);
            Framebuffer framebuffer = RF.CreateFramebuffer(new FramebufferDescription(null, finalOutput));

            ResourceLayout computeLayout = RF.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("ComputeOutput", ResourceKind.TextureReadWrite, ShaderStages.Compute)));
            ResourceSet computeSet = RF.CreateResourceSet(new ResourceSetDescription(computeLayout, computeOutput));

            Pipeline computePipeline = RF.CreateComputePipeline(new ComputePipelineDescription(
                TestShaders.LoadCompute(RF, "ComputeTextureGenerator"),
                computeLayout,
                4, 1, 1));

            ResourceLayout graphicsLayout = RF.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Input", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("InputSampler", ResourceKind.Sampler, ShaderStages.Fragment)));
            ResourceSet graphicsSet = RF.CreateResourceSet(new ResourceSetDescription(graphicsLayout, computeOutput, GD.PointSampler));

            Pipeline graphicsPipeline = RF.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.CullNone,
                PrimitiveTopology.TriangleStrip,
                new ShaderSetDescription(
                    Array.Empty<VertexLayoutDescription>(),
                    TestShaders.LoadVertexFragment(RF, "FullScreenBlit")),
                graphicsLayout,
                framebuffer.OutputDescription));

            CommandList cl = RF.CreateCommandList();
            cl.Begin();
            cl.SetPipeline(computePipeline);
            cl.SetComputeResourceSet(0, computeSet);
            cl.Dispatch(1, 1, 1);
            cl.SetFramebuffer(framebuffer);
            cl.ClearColorTarget(0, new RgbaFloat());
            cl.SetPipeline(graphicsPipeline);
            cl.SetGraphicsResourceSet(0, graphicsSet);
            cl.Draw(4);
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();

            Texture readback = GetReadback(finalOutput);
            MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(readback, MapMode.Read);
            Assert.Equal(RgbaFloat.Red, readView[0, 0]);
            Assert.Equal(RgbaFloat.Green, readView[1, 0]);
            Assert.Equal(RgbaFloat.Blue, readView[2, 0]);
            Assert.Equal(RgbaFloat.White, readView[3, 0]);
            GD.Unmap(readback);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SampleTexture1D(bool arrayTexture)
        {
            if (!GD.Features.Texture1D) { return; }

            Texture target = RF.CreateTexture(TextureDescription.Texture2D(
                50, 50, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.RenderTarget));
            Texture staging = RF.CreateTexture(TextureDescription.Texture2D(
                50, 50, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Staging));

            Framebuffer framebuffer = RF.CreateFramebuffer(new FramebufferDescription(null, target));

            string SetName = arrayTexture ? "FullScreenTriSampleTextureArray" : "FullScreenTriSampleTexture";
            ShaderSetDescription shaderSet = new ShaderSetDescription(
                Array.Empty<VertexLayoutDescription>(),
                TestShaders.LoadVertexFragment(RF, SetName));

            uint layers = arrayTexture ? 10u : 1u;
            Texture tex1D = RF.CreateTexture(
                TextureDescription.Texture1D(128, 1, layers, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Sampled));
            RgbaFloat[] colors = new RgbaFloat[tex1D.Width];
            for (int i = 0; i < colors.Length; i++) { colors[i] = RgbaFloat.Pink; }
            GD.UpdateTexture(tex1D, colors, 0, 0, 0, tex1D.Width, 1, 1, 0, 0);

            ResourceLayout layout = RF.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Tex", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("Smp", ResourceKind.Sampler, ShaderStages.Fragment)));

            ResourceSet set = RF.CreateResourceSet(new ResourceSetDescription(layout, tex1D, GD.PointSampler));

            GraphicsPipelineDescription gpd = new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.CullNone,
                PrimitiveTopology.TriangleList,
                shaderSet,
                layout,
                framebuffer.OutputDescription);

            Pipeline pipeline = RF.CreateGraphicsPipeline(ref gpd);

            CommandList cl = RF.CreateCommandList();

            cl.Begin();
            cl.SetFramebuffer(framebuffer);
            cl.SetFullViewports();
            cl.SetFullScissorRects();
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.SetPipeline(pipeline);
            cl.SetGraphicsResourceSet(0, set);
            cl.Draw(3);
            cl.CopyTexture(target, staging);
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();

            MappedResourceView<RgbaFloat> readView = GD.Map<RgbaFloat>(staging, MapMode.Read);
            for (int x = 0; x < staging.Width; x++)
            {
                Assert.Equal(RgbaFloat.Pink, readView[x, 0]);
            }
            GD.Unmap(staging);
        }
    }

#if TEST_OPENGL
    public class OpenGLRenderTests : RenderTests<OpenGLDeviceCreator> { }
#endif
#if TEST_OPENGLES
    public class OpenGLESRenderTests : RenderTests<OpenGLESDeviceCreator> { }
#endif
#if TEST_VULKAN
    public class VulkanRenderTests : RenderTests<VulkanDeviceCreator> { }
#endif
#if TEST_D3D11
    public class D3D11RenderTests : RenderTests<D3D11DeviceCreator> { }
#endif
#if TEST_METAL
        public class MetalRenderTests : RenderTests<MetalDeviceCreator> { }
#endif
}
