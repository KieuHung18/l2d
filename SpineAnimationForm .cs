using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Spine;
using System;
using System.IO;
using System.Windows.Forms;

namespace SpineAnimationViewer
{
    public class SpineAnimationForm : Form
    {
        private SKGLControl skControl;
        private Skeleton skeleton;
        private AnimationState animationState;
        private Atlas atlas;
        private System.Windows.Forms.Timer animationTimer;
        private float deltaTime = 0;
        private SkeletonRenderer skeletonRenderer;

        public SpineAnimationForm()
        {
            
            this.Text = "Spine Animation Viewer";
            this.Width = 800;
            this.Height = 600;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Setup SkiaSharp OpenGL control
            skControl = new SKGLControl();
            skControl.Dock = DockStyle.Fill;
            skControl.PaintSurface += OnPaintSurface;
            this.Controls.Add(skControl);

            // Load Spine data
            try
            {
                LoadSpineData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading Spine data: {ex.Message}",
                    "Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Setup animation timer (60 FPS)
            animationTimer = new System.Windows.Forms.Timer();
            animationTimer.Interval = 16; // ~60 FPS
            animationTimer.Tick += UpdateAnimation;
            animationTimer.Start();
        }

        private void LoadSpineData()
        {
            // TODO: Replace these paths with your actual file paths
            string atlasPath = "Assets/character.atlas";
            string skelPath = "Assets/character.skel";
            string animationName = "walk"; // Replace with your animation name

            // Load atlas with custom texture loader
            atlas = new Atlas(atlasPath, new SkiaSharpTextureLoader());

            // Load skeleton binary data
            SkeletonBinary binary = new SkeletonBinary(atlas);
            binary.Scale = 1.0f; // Adjust scale if needed
            SkeletonData skeletonData = binary.ReadSkeletonData(skelPath);

            // Create skeleton instance
            skeleton = new Skeleton(skeletonData);
            skeleton.SetToSetupPose();

            // Position skeleton at center of screen
            skeleton.X = this.Width / 2;
            skeleton.Y = this.Height - 100; // Near bottom

            skeleton.UpdateWorldTransform();

            // Setup animation state
            AnimationStateData stateData = new AnimationStateData(skeletonData);
            animationState = new AnimationState(stateData);

            // Set initial animation (loop = true)
            if (skeletonData.Animations.Count > 0)
            {
                // Try to play the specified animation, or first available
                try
                {
                    animationState.SetAnimation(0, animationName, true);
                }
                catch
                {
                    // If animation not found, play first animation
                    animationState.SetAnimation(0, skeletonData.Animations.Items[0].Name, true);
                }
            }

            // Create skeleton renderer
            skeletonRenderer = new SkeletonRenderer();
        }

        private void UpdateAnimation(object sender, EventArgs e)
        {
            if (skeleton == null || animationState == null)
                return;

            deltaTime = 0.016f; // 16ms for 60 FPS

            // Update animation state
            animationState.Update(deltaTime);
            animationState.Apply(skeleton);
            skeleton.UpdateWorldTransform();

            // Trigger redraw
            skControl.Invalidate();
        }

        private void OnPaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
        {
            SKCanvas canvas = e.Surface.Canvas;

            // Clear background
            canvas.Clear(new SKColor(100, 149, 237)); // Cornflower blue

            if (skeleton == null)
                return;

            // Render the skeleton
            RenderSkeleton(canvas, skeleton);
        }

        private void RenderSkeleton(SKCanvas canvas, Skeleton skeleton)
        {
            // Save canvas state
            canvas.Save();

            // Iterate through slots in draw order
            foreach (Slot slot in skeleton.DrawOrder)
            {
                if (slot.Attachment == null)
                    continue;

                // Handle region attachments (sprites)
                if (slot.Attachment is RegionAttachment regionAttachment)
                {
                    RenderRegionAttachment(canvas, slot, regionAttachment);
                }
                // Handle mesh attachments
                else if (slot.Attachment is MeshAttachment meshAttachment)
                {
                    RenderMeshAttachment(canvas, slot, meshAttachment);
                }
            }

            // Restore canvas state
            canvas.Restore();
        }

        private void RenderRegionAttachment(SKCanvas canvas, Slot slot, RegionAttachment attachment)
        {
            // Get the atlas region
            AtlasRegion region = (AtlasRegion)attachment.RendererObject;
            if (region == null)
                return;

            // Get texture
            SKBitmap texture = (SKBitmap)region.page.rendererObject;
            if (texture == null)
                return;

            // Compute world vertices (8 floats: x,y for 4 corners)
            float[] vertices = new float[8];
            attachment.ComputeWorldVertices(slot.Bone, vertices);

            // Create paint with slot color and alpha
            using (SKPaint paint = new SKPaint())
            {
                paint.IsAntialias = true;
                paint.FilterQuality = SKFilterQuality.High;

                // Apply slot color tint
                byte r = (byte)(slot.R * 255);
                byte g = (byte)(slot.G * 255);
                byte b = (byte)(slot.B * 255);
                byte a = (byte)(slot.A * 255);
                paint.Color = new SKColor(r, g, b, a);

                // Get texture coordinates from atlas region
                SKRect srcRect = new SKRect(
                    region.x,
                    region.y,
                    region.x + region.width,
                    region.y + region.height
                );

                // Calculate destination quad from vertices
                // vertices: [x0,y0, x1,y1, x2,y2, x3,y3]
                // Order: bottom-left, bottom-right, top-right, top-left

                canvas.Save();

                // Create transformation matrix from vertices
                // This is a simplified approach - for accurate rendering,
                // you'd need to use a proper quad-to-quad transformation
                float centerX = (vertices[0] + vertices[4]) / 2;
                float centerY = (vertices[1] + vertices[5]) / 2;
                float width = Math.Abs(vertices[4] - vertices[0]);
                float height = Math.Abs(vertices[1] - vertices[5]);

                SKRect destRect = new SKRect(
                    centerX - width / 2,
                    centerY - height / 2,
                    centerX + width / 2,
                    centerY + height / 2
                );

                // Draw the sprite region
                canvas.DrawBitmap(texture, srcRect, destRect, paint);

                canvas.Restore();
            }
        }

        private void RenderMeshAttachment(SKCanvas canvas, Slot slot, MeshAttachment attachment)
        {
            // Get the atlas region
            AtlasRegion region = (AtlasRegion)attachment.RendererObject;
            if (region == null)
                return;

            // Get texture
            SKBitmap texture = (SKBitmap)region.page.rendererObject;
            if (texture == null)
                return;

            // Compute world vertices
            float[] vertices = new float[attachment.WorldVerticesLength];
            attachment.ComputeWorldVertices(slot, vertices);

            // Create paint with slot color
            using (SKPaint paint = new SKPaint())
            {
                paint.IsAntialias = true;
                paint.FilterQuality = SKFilterQuality.High;

                byte r = (byte)(slot.R * 255);
                byte g = (byte)(slot.G * 255);
                byte b = (byte)(slot.B * 255);
                byte a = (byte)(slot.A * 255);
                paint.Color = new SKColor(r, g, b, a);

                // For mesh rendering, you'd need to triangulate and draw each triangle
                // This is a simplified version - full implementation would use triangles

                // Draw a simple approximation using the bounding box
                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;

                for (int i = 0; i < vertices.Length; i += 2)
                {
                    minX = Math.Min(minX, vertices[i]);
                    maxX = Math.Max(maxX, vertices[i]);
                    minY = Math.Min(minY, vertices[i + 1]);
                    maxY = Math.Max(maxY, vertices[i + 1]);
                }

                SKRect srcRect = new SKRect(region.x, region.y,
                    region.x + region.width, region.y + region.height);
                SKRect destRect = new SKRect(minX, minY, maxX, maxY);

                canvas.DrawBitmap(texture, srcRect, destRect, paint);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Stop timer
            if (animationTimer != null)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
            }

            // Cleanup resources
            atlas?.Dispose();

            base.OnFormClosing(e);
        }
    }

    // Custom texture loader for SkiaSharp
    public class SkiaSharpTextureLoader : TextureLoader
    {
        public void Load(AtlasPage page, string path)
        {
            // Load the PNG file as SKBitmap
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Texture file not found: {path}");
            }

            SKBitmap bitmap = SKBitmap.Decode(path);

            if (bitmap == null)
            {
                throw new Exception($"Failed to decode texture: {path}");
            }

            // Store bitmap in the page's renderer object
            page.rendererObject = bitmap;
            page.width = bitmap.Width;
            page.height = bitmap.Height;
        }

        public void Unload(object texture)
        {
            // Dispose the bitmap when unloading
            if (texture is SKBitmap bitmap)
            {
                bitmap.Dispose();
            }
        }
    }

    // Simple skeleton renderer helper
    public class SkeletonRenderer
    {
        public SkeletonRenderer()
        {
            // Initialize renderer settings if needed
        }

        public void Draw(SKCanvas canvas, Skeleton skeleton)
        {
            // This is handled in the main form's RenderSkeleton method
            // Kept here for compatibility with Spine API patterns
        }
    }
}