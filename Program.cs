using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using Szeminarium;

namespace GrafikaLAB02
{
    internal class Program
    {
        private static IWindow graphicWindow;
        private static GL Gl;

        //minden kockahoz tartozik egy ModelObjectDescriptor
        private static List<ModelObjectDescriptor> cubes;

        private static CameraDescriptor camera = new CameraDescriptor();
        // A forgatási mátrix, amit a rendereléshez használunk. Ez határozza meg a forgatott lap transzformációját.
        private static Matrix4X4<float> rotationMatrix = Matrix4X4<float>.Identity;
        // Jelzi, hogy éppen forgatunk-e vagy sem (animáció folyamatban van-e)
        private static bool rotating = false;
        // Az aktuális forgatási szög radiánban (gyűjtjük frame-ről frame-re)
        private static float rotationAngle = 0f;
        // A forgatás sebessége radián/másodperc — itt π/2, tehát 90 fok 1 másodperc alatt
        private static float rotationSpeed = (float)Math.PI / 2; // 90 fok egy másodperc alatt
        // Megmondja, hogy előre (`Space`) vagy visszafelé (`Backspace`) forgatunk
        private static bool rotateBack = false;


        private const string ModelMatrixVariableName = "uModel";
        private const string ViewMatrixVariableName = "uView";
        private const string ProjectionMatrixVariableName = "uProjection";

        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
        layout (location = 1) in vec4 vCol;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;


        out vec4 outCol;

        void main()
        {
            outCol = vCol;
            gl_Position = uProjection * uView * uModel * vec4(vPos, 1.0);
        }
        ";

        private static readonly string FragmentShaderSource = @"
        #version 330 core
        out vec4 FragColor;
        in vec4 outCol;

        void main()
        {
            FragColor = outCol;
        }
        ";

        private static uint program;

        //definialom elore az alap szineket, belul fekete lesz
        private static float[] White = { 1f, 1f, 1f, 1f };
        private static float[] Yellow = { 1f, 1f, 0f, 1f };
        private static float[] Red = { 1f, 0f, 0f, 1f };
        private static float[] Orange = { 1f, 0.5f, 0f, 1f };
        private static float[] Blue = { 0f, 0f, 1f, 1f };
        private static float[] Green = { 0f, 1f, 0f, 1f };
        private static float[] Black = { 0f, 0f, 0f, 1f };

        //minden kis kockahoz epitunk egy szinmatrixot
        private static float[] CreateColorArray(float[] top, float[] bottom, float[] front, float[] back, float[] left, float[] right)
        {
            List<float> colors = new();
            for (int i = 0; i < 4; i++) colors.AddRange(top);
            for (int i = 0; i < 4; i++) colors.AddRange(front);
            for (int i = 0; i < 4; i++) colors.AddRange(left);
            for (int i = 0; i < 4; i++) colors.AddRange(bottom);
            for (int i = 0; i < 4; i++) colors.AddRange(back);
            for (int i = 0; i < 4; i++) colors.AddRange(right);
            return colors.ToArray();
        }

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "Rubik's Cube Renderer";
            windowOptions.Size = new Vector2D<int>(500, 500);

            graphicWindow = Window.Create(windowOptions);

            graphicWindow.Load += GraphicWindow_Load;
            graphicWindow.Update += GraphicWindow_Update;
            graphicWindow.Render += GraphicWindow_Render;
            graphicWindow.Closing += GraphicWindow_Closing;

            graphicWindow.Run();
        }

        private static void GraphicWindow_Closing()
        {
            foreach (var cube in cubes)
                cube.Dispose();
            Gl.DeleteProgram(program);
        }

        private static void GraphicWindow_Load()
        {
            Gl = graphicWindow.CreateOpenGL();
            var inputContext = graphicWindow.CreateInput();
            foreach (var keyboard in inputContext.Keyboards)
                keyboard.KeyDown += Keyboard_KeyDown;

            Gl.ClearColor(System.Drawing.Color.White);
            Gl.Enable(EnableCap.CullFace);
            Gl.CullFace(TriangleFace.Back);
            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);

            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Gl.ShaderSource(vshader, VertexShaderSource);
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception("Vertex shader failed to compile: " + Gl.GetShaderInfoLog(vshader));

            Gl.ShaderSource(fshader, FragmentShaderSource);
            Gl.CompileShader(fshader);
            Gl.GetShader(fshader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True)
                throw new Exception("Fragment shader failed to compile: " + Gl.GetShaderInfoLog(fshader));

            program = Gl.CreateProgram();
            Gl.AttachShader(program, vshader);
            Gl.AttachShader(program, fshader);
            Gl.LinkProgram(program);

            Gl.DetachShader(program, vshader);
            Gl.DetachShader(program, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);

            Gl.GetProgram(program, GLEnum.LinkStatus, out var status);
            if (status == 0)
                Console.WriteLine("Error linking shader " + Gl.GetProgramInfoLog(program));

            cubes = new();
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        // ez donti el hogy egy kis kocka melyik oldalon van, es ennek alapjan milyen szint kapjon
                        /*
                         ha y == 1 akkor felso sor, tehta felso oldal
                        ha y == -1, akkor also sor
                        z == 1, akkor elulso reteg
                        z == -1, hatso reteg
                        x == -1, bal oldal (kek)
                        x == 1, akkor jobb oldal, zold
                         */
                        float[] top = (y == 1) ? White : Black;
                        float[] bottom = (y == -1) ? Yellow : Black;
                        float[] front = (z == 1) ? Red : Black;
                        float[] back = (z == -1) ? Orange : Black;
                        float[] left = (x == -1) ? Blue : Black;
                        float[] right = (x == 1) ? Green : Black;

                        float[] colors = CreateColorArray(top, bottom, front, back, left, right);
                        cubes.Add(ModelObjectDescriptor.CreateCube(Gl, colors));
                        //itt adjuk hozza a szineket az oldalakhoz
                    }
                }
            }
        }

        private static void Keyboard_KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            switch (key)
            {
                case Key.Left: camera.DecreaseZYAngle(); break;
                case Key.Right: camera.IncreaseZYAngle(); break;
                case Key.Down: camera.DecreaseZXAngle(); break; // modositottam hogy lassam a tetejet es az aljat is
                case Key.Up: camera.IncreaseZXAngle(); break;
                case Key.W: camera.IncreaseDistance(); break;
                case Key.S: camera.DecreaseDistance(); break;
                case Key.A: camera.moveForward(0.1f); break;
                case Key.D: camera.moveRight(0.1f); break;
                case Key.Q: camera.moveForward(-0.1f); break;
                case Key.R: camera.moveRight(-0.1f); break;
                case Key.Space: //animacio kezelese
                    if (!rotating)
                    {
                        rotating = true;
                        rotateBack = false;
                        rotationAngle = 0f;
                    }
                    break;
                case Key.Backspace:
                    if (!rotating)
                    {
                        rotating = true;
                        rotateBack = true;
                        rotationAngle = 0f;
                    }
                    break;

            }
        }

        private static void GraphicWindow_Update(double deltaTime) {

            if (!rotating) return;

            // Mennyi szöggel kell elforgatni ebben a frame-ben (sebesség * eltelt idő)
            float deltaAngle = (float)(rotationSpeed * deltaTime);
            // Növeljük az eddigi összforgatási szöget
            rotationAngle += deltaAngle;

            if (rotationAngle >= Math.PI / 2f)
            {
                // Pontosítunk, hogy ne lépjük túl a 90 fokot
                deltaAngle -= (rotationAngle - (float)Math.PI / 2f);
                rotating = false;
            }
            // Ha visszafelé kell forogni, a szög negatív
            float angle = rotateBack ? -deltaAngle : deltaAngle;
            // Frissítjük a forgatási mátrixot a delta szöggel Y tengely körül
            rotationMatrix *= Matrix4X4.CreateFromAxisAngle(Vector3D<float>.UnitY, angle); // pl. Y tengely mentén forgatás (felső lap)
        }

        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            Gl.Clear(ClearBufferMask.ColorBufferBit);
            Gl.Clear(ClearBufferMask.DepthBufferBit);

            Gl.UseProgram(program);

            var viewMatrix = Matrix4X4.CreateLookAt(camera.Position, camera.Target, camera.UpVector);
            SetMatrix(viewMatrix, ViewMatrixVariableName);

            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)(Math.PI / 2), 1f, 0.1f, 100f);
            SetMatrix(projectionMatrix, ProjectionMatrixVariableName);

            float spacing = 0.33f;
            int i = 0;
            // itt rajzoltatom ki  akis kockakat
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector3D<float> pos = new(x * spacing, y * spacing, z * spacing);
                        var translate = Matrix4X4.CreateTranslation(pos);
                        var scale = Matrix4X4.CreateScale(0.3f);
                        var model = scale * translate;
                        // feltételezzük hogy a felső (y == 1) lapot forgatjuk
                        // Ha a kocka a forgó lapon van (pl. felső sor), akkor alkalmazzuk a forgatást
                        bool isRotatingLayer = (y == 1);
                        var finalModel = isRotatingLayer ? model * rotationMatrix : model;
                        // Beállítjuk a model mátrixot shaderbe
                        SetMatrix(finalModel, ModelMatrixVariableName);
                        DrawModelObject(cubes[i++]);
                    }
                }
            }
        }

        private static unsafe void DrawModelObject(ModelObjectDescriptor modelObject)
        {
            Gl.BindVertexArray(modelObject.Vao);
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, modelObject.Indices);
            Gl.DrawElements(PrimitiveType.Triangles, modelObject.IndexArrayLength, DrawElementsType.UnsignedInt, null);
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);
            Gl.BindVertexArray(0);
        }

        private static unsafe void SetMatrix(Matrix4X4<float> mx, string uniformName)
        {
            int location = Gl.GetUniformLocation(program, uniformName);
            if (location == -1)
                throw new Exception($"{uniformName} uniform not found on shader.");

            Gl.UniformMatrix4(location, 1, false, (float*)&mx);
            CheckError();
        }

        public static void CheckError()
        {
            var error = (ErrorCode)Gl.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception("GL.GetError() returned " + error.ToString());
        }
    }
}
