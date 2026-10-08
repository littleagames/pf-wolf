using Avalonia.OpenGL;

namespace PFWolf.Editor.Rendering;

/// <summary>
/// The OpenGL calls the 3D view makes, looked up through Avalonia's context. The same calls
/// exist in OpenGL ES 3 (ANGLE, Avalonia's default on Windows) and desktop OpenGL 3.3.
/// </summary>
public sealed unsafe class Gl
{
    public const int COLOR_BUFFER_BIT = 0x4000, DEPTH_BUFFER_BIT = 0x0100;
    public const int DEPTH_TEST = 0x0B71, CULL_FACE = 0x0B44, BLEND = 0x0BE2, POLYGON_OFFSET_FILL = 0x8037;
    public const int BACK = 0x0405, LEQUAL = 0x0203, LESS = 0x0201;
    public const int SRC_ALPHA = 0x0302, ONE_MINUS_SRC_ALPHA = 0x0303;
    public const int ARRAY_BUFFER = 0x8892, STATIC_DRAW = 0x88E4, DYNAMIC_DRAW = 0x88E8;
    public const int FLOAT = 0x1406, UNSIGNED_BYTE = 0x1401, TRIANGLES = 0x0004, LINES = 0x0001;
    public const int VERTEX_SHADER = 0x8B31, FRAGMENT_SHADER = 0x8B30, COMPILE_STATUS = 0x8B81, LINK_STATUS = 0x8B82;
    public const int TEXTURE_2D = 0x0DE1, TEXTURE0 = 0x84C0, RGBA = 0x1908, RGBA8 = 0x8058;
    public const int TEXTURE_MIN_FILTER = 0x2801, TEXTURE_MAG_FILTER = 0x2800, TEXTURE_WRAP_S = 0x2802, TEXTURE_WRAP_T = 0x2803;
    public const int NEAREST = 0x2600, REPEAT = 0x2901, UNPACK_ALIGNMENT = 0x0CF5;

    private readonly delegate* unmanaged<float, float, float, float, void> _clearColor;
    private readonly delegate* unmanaged<int, void> _clear;
    private readonly delegate* unmanaged<int, int, int, int, void> _viewport;
    private readonly delegate* unmanaged<int, void> _enable;
    private readonly delegate* unmanaged<int, void> _disable;
    private readonly delegate* unmanaged<int, void> _depthFunc;
    private readonly delegate* unmanaged<byte, void> _depthMask;
    private readonly delegate* unmanaged<int, void> _cullFace;
    private readonly delegate* unmanaged<int, int, void> _blendFunc;
    private readonly delegate* unmanaged<float, float, void> _polygonOffset;
    private readonly delegate* unmanaged<int, int*, void> _genBuffers;
    private readonly delegate* unmanaged<int, int*, void> _deleteBuffers;
    private readonly delegate* unmanaged<int, int, void> _bindBuffer;
    private readonly delegate* unmanaged<int, nint, void*, int, void> _bufferData;
    private readonly delegate* unmanaged<int, int*, void> _genVertexArrays;
    private readonly delegate* unmanaged<int, int*, void> _deleteVertexArrays;
    private readonly delegate* unmanaged<int, void> _bindVertexArray;
    private readonly delegate* unmanaged<int, int, int, byte, int, nint, void> _vertexAttribPointer;
    private readonly delegate* unmanaged<int, void> _enableVertexAttribArray;
    private readonly delegate* unmanaged<int, int> _createShader;
    private readonly delegate* unmanaged<int, void> _deleteShader;
    private readonly delegate* unmanaged<int, int, byte**, int*, void> _shaderSource;
    private readonly delegate* unmanaged<int, void> _compileShader;
    private readonly delegate* unmanaged<int, int, int*, void> _getShaderiv;
    private readonly delegate* unmanaged<int, int, int*, byte*, void> _getShaderInfoLog;
    private readonly delegate* unmanaged<int> _createProgram;
    private readonly delegate* unmanaged<int, void> _deleteProgram;
    private readonly delegate* unmanaged<int, int, void> _attachShader;
    private readonly delegate* unmanaged<int, int, byte*, void> _bindAttribLocation;
    private readonly delegate* unmanaged<int, void> _linkProgram;
    private readonly delegate* unmanaged<int, int, int*, void> _getProgramiv;
    private readonly delegate* unmanaged<int, int, int*, byte*, void> _getProgramInfoLog;
    private readonly delegate* unmanaged<int, void> _useProgram;
    private readonly delegate* unmanaged<int, byte*, int> _getUniformLocation;
    private readonly delegate* unmanaged<int, int, byte, float*, void> _uniformMatrix4fv;
    private readonly delegate* unmanaged<int, int, void> _uniform1i;
    private readonly delegate* unmanaged<int, float, void> _uniform1f;
    private readonly delegate* unmanaged<int, float, float, float, float, void> _uniform4f;
    private readonly delegate* unmanaged<int, int*, void> _genTextures;
    private readonly delegate* unmanaged<int, int*, void> _deleteTextures;
    private readonly delegate* unmanaged<int, int, void> _bindTexture;
    private readonly delegate* unmanaged<int, void> _activeTexture;
    private readonly delegate* unmanaged<int, int, int, int, int, int, int, int, void*, void> _texImage2D;
    private readonly delegate* unmanaged<int, int, int, void> _texParameteri;
    private readonly delegate* unmanaged<int, int, void> _pixelStorei;
    private readonly delegate* unmanaged<int, int, int, void> _drawArrays;
    private readonly delegate* unmanaged<float, void> _lineWidth;

    public Gl(GlInterface gl)
    {
        nint Get(string name)
        {
            var address = gl.GetProcAddress(name);
            return address != 0 ? address : throw new InvalidOperationException($"OpenGL has no {name}");
        }

        _clearColor = (delegate* unmanaged<float, float, float, float, void>)Get("glClearColor");
        _clear = (delegate* unmanaged<int, void>)Get("glClear");
        _viewport = (delegate* unmanaged<int, int, int, int, void>)Get("glViewport");
        _enable = (delegate* unmanaged<int, void>)Get("glEnable");
        _disable = (delegate* unmanaged<int, void>)Get("glDisable");
        _depthFunc = (delegate* unmanaged<int, void>)Get("glDepthFunc");
        _depthMask = (delegate* unmanaged<byte, void>)Get("glDepthMask");
        _cullFace = (delegate* unmanaged<int, void>)Get("glCullFace");
        _blendFunc = (delegate* unmanaged<int, int, void>)Get("glBlendFunc");
        _polygonOffset = (delegate* unmanaged<float, float, void>)Get("glPolygonOffset");
        _genBuffers = (delegate* unmanaged<int, int*, void>)Get("glGenBuffers");
        _deleteBuffers = (delegate* unmanaged<int, int*, void>)Get("glDeleteBuffers");
        _bindBuffer = (delegate* unmanaged<int, int, void>)Get("glBindBuffer");
        _bufferData = (delegate* unmanaged<int, nint, void*, int, void>)Get("glBufferData");
        _genVertexArrays = (delegate* unmanaged<int, int*, void>)Get("glGenVertexArrays");
        _deleteVertexArrays = (delegate* unmanaged<int, int*, void>)Get("glDeleteVertexArrays");
        _bindVertexArray = (delegate* unmanaged<int, void>)Get("glBindVertexArray");
        _vertexAttribPointer = (delegate* unmanaged<int, int, int, byte, int, nint, void>)Get("glVertexAttribPointer");
        _enableVertexAttribArray = (delegate* unmanaged<int, void>)Get("glEnableVertexAttribArray");
        _createShader = (delegate* unmanaged<int, int>)Get("glCreateShader");
        _deleteShader = (delegate* unmanaged<int, void>)Get("glDeleteShader");
        _shaderSource = (delegate* unmanaged<int, int, byte**, int*, void>)Get("glShaderSource");
        _compileShader = (delegate* unmanaged<int, void>)Get("glCompileShader");
        _getShaderiv = (delegate* unmanaged<int, int, int*, void>)Get("glGetShaderiv");
        _getShaderInfoLog = (delegate* unmanaged<int, int, int*, byte*, void>)Get("glGetShaderInfoLog");
        _createProgram = (delegate* unmanaged<int>)Get("glCreateProgram");
        _deleteProgram = (delegate* unmanaged<int, void>)Get("glDeleteProgram");
        _attachShader = (delegate* unmanaged<int, int, void>)Get("glAttachShader");
        _bindAttribLocation = (delegate* unmanaged<int, int, byte*, void>)Get("glBindAttribLocation");
        _linkProgram = (delegate* unmanaged<int, void>)Get("glLinkProgram");
        _getProgramiv = (delegate* unmanaged<int, int, int*, void>)Get("glGetProgramiv");
        _getProgramInfoLog = (delegate* unmanaged<int, int, int*, byte*, void>)Get("glGetProgramInfoLog");
        _useProgram = (delegate* unmanaged<int, void>)Get("glUseProgram");
        _getUniformLocation = (delegate* unmanaged<int, byte*, int>)Get("glGetUniformLocation");
        _uniformMatrix4fv = (delegate* unmanaged<int, int, byte, float*, void>)Get("glUniformMatrix4fv");
        _uniform1i = (delegate* unmanaged<int, int, void>)Get("glUniform1i");
        _uniform1f = (delegate* unmanaged<int, float, void>)Get("glUniform1f");
        _uniform4f = (delegate* unmanaged<int, float, float, float, float, void>)Get("glUniform4f");
        _genTextures = (delegate* unmanaged<int, int*, void>)Get("glGenTextures");
        _deleteTextures = (delegate* unmanaged<int, int*, void>)Get("glDeleteTextures");
        _bindTexture = (delegate* unmanaged<int, int, void>)Get("glBindTexture");
        _activeTexture = (delegate* unmanaged<int, void>)Get("glActiveTexture");
        _texImage2D = (delegate* unmanaged<int, int, int, int, int, int, int, int, void*, void>)Get("glTexImage2D");
        _texParameteri = (delegate* unmanaged<int, int, int, void>)Get("glTexParameteri");
        _pixelStorei = (delegate* unmanaged<int, int, void>)Get("glPixelStorei");
        _drawArrays = (delegate* unmanaged<int, int, int, void>)Get("glDrawArrays");
        _lineWidth = (delegate* unmanaged<float, void>)Get("glLineWidth");
    }

    public void ClearColor(float r, float g, float b, float a) => _clearColor(r, g, b, a);
    public void Clear(int mask) => _clear(mask);
    public void Viewport(int x, int y, int width, int height) => _viewport(x, y, width, height);
    public void Enable(int cap) => _enable(cap);
    public void Disable(int cap) => _disable(cap);
    public void DepthFunc(int func) => _depthFunc(func);
    public void DepthMask(bool write) => _depthMask(write ? (byte)1 : (byte)0);
    public void CullFace(int mode) => _cullFace(mode);
    public void BlendFunc(int source, int destination) => _blendFunc(source, destination);
    public void PolygonOffset(float factor, float units) => _polygonOffset(factor, units);
    public void LineWidth(float width) => _lineWidth(width);

    public int GenBuffer()
    {
        int id;
        _genBuffers(1, &id);
        return id;
    }

    public void DeleteBuffer(int id) => _deleteBuffers(1, &id);
    public void BindBuffer(int target, int id) => _bindBuffer(target, id);

    public void BufferData(int target, ReadOnlySpan<float> data, int usage)
    {
        fixed (float* pointer = data)
            _bufferData(target, data.Length * sizeof(float), pointer, usage);
    }

    public int GenVertexArray()
    {
        int id;
        _genVertexArrays(1, &id);
        return id;
    }

    public void DeleteVertexArray(int id) => _deleteVertexArrays(1, &id);
    public void BindVertexArray(int id) => _bindVertexArray(id);

    public void VertexAttribPointer(int index, int size, int stride, int offset)
        => _vertexAttribPointer(index, size, FLOAT, 0, stride, offset);

    public void EnableVertexAttribArray(int index) => _enableVertexAttribArray(index);

    /// <summary>Compiles and links a program, binding the attributes to their indices; throws with the log on failure</summary>
    public int MakeProgram(string vertexSource, string fragmentSource, params string[] attributes)
    {
        int vertex = CompileShader(VERTEX_SHADER, vertexSource);
        int fragment = CompileShader(FRAGMENT_SHADER, fragmentSource);
        int program = _createProgram();
        _attachShader(program, vertex);
        _attachShader(program, fragment);
        for (int i = 0; i < attributes.Length; i++)
        {
            var name = System.Text.Encoding.ASCII.GetBytes(attributes[i] + "\0");
            fixed (byte* pointer = name)
                _bindAttribLocation(program, i, pointer);
        }
        _linkProgram(program);
        _deleteShader(vertex);
        _deleteShader(fragment);

        int status;
        _getProgramiv(program, LINK_STATUS, &status);
        if (status == 0)
        {
            var log = InfoLog(program, _getProgramInfoLog);
            _deleteProgram(program);
            throw new InvalidOperationException($"Linking the 3D view's shaders failed: {log}");
        }
        return program;
    }

    private int CompileShader(int type, string source)
    {
        int shader = _createShader(type);
        var bytes = System.Text.Encoding.UTF8.GetBytes(source);
        fixed (byte* pointer = bytes)
        {
            byte* text = pointer;
            int length = bytes.Length;
            _shaderSource(shader, 1, &text, &length);
        }
        _compileShader(shader);

        int status;
        _getShaderiv(shader, COMPILE_STATUS, &status);
        if (status == 0)
        {
            var log = InfoLog(shader, _getShaderInfoLog);
            _deleteShader(shader);
            throw new InvalidOperationException($"Compiling a 3D view shader failed: {log}");
        }
        return shader;
    }

    private static string InfoLog(int id, delegate* unmanaged<int, int, int*, byte*, void> getLog)
    {
        var buffer = new byte[4096];
        int length;
        fixed (byte* pointer = buffer)
            getLog(id, buffer.Length, &length, pointer);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, Math.Clamp(length, 0, buffer.Length));
    }

    public void DeleteProgram(int id) => _deleteProgram(id);
    public void UseProgram(int id) => _useProgram(id);

    public int GetUniformLocation(int program, string name)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(name + "\0");
        fixed (byte* pointer = bytes)
            return _getUniformLocation(program, pointer);
    }

    public void UniformMatrix4(int location, System.Numerics.Matrix4x4 matrix)
        => _uniformMatrix4fv(location, 1, 0, &matrix.M11);

    public void Uniform1(int location, int value) => _uniform1i(location, value);
    public void Uniform1(int location, float value) => _uniform1f(location, value);
    public void Uniform4(int location, float x, float y, float z, float w) => _uniform4f(location, x, y, z, w);

    public int GenTexture()
    {
        int id;
        _genTextures(1, &id);
        return id;
    }

    public void DeleteTexture(int id) => _deleteTextures(1, &id);
    public void BindTexture(int target, int id) => _bindTexture(target, id);
    public void ActiveTexture(int unit) => _activeTexture(unit);
    public void TexParameter(int target, int name, int value) => _texParameteri(target, name, value);

    /// <summary>Uploads RGBA pixels, row by row from the top</summary>
    public void TexImage2D(int width, int height, byte[] rgba)
    {
        _pixelStorei(UNPACK_ALIGNMENT, 1);
        fixed (byte* pointer = rgba)
            _texImage2D(TEXTURE_2D, 0, RGBA8, width, height, 0, RGBA, UNSIGNED_BYTE, pointer);
    }

    public void DrawArrays(int mode, int first, int count) => _drawArrays(mode, first, count);
}
