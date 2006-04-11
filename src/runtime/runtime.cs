// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Python.Runtime {

    [SuppressUnmanagedCodeSecurityAttribute()]

    public class Runtime {

	/// <summary>
	/// Encapsulates the low-level Python C API. Note that it is
	/// the responsibility of the caller to have acquired the GIL
	/// before calling any of these methods.
	/// </summary>

	internal const string dll = "python24";
	internal static bool wrap_exceptions;
	internal static bool is32bit;

	internal static void Initialize() {

	    is32bit = IntPtr.Size == 4;

	    Runtime.Py_Initialize();
	    Runtime.PyEval_InitThreads();

	    IntPtr dict = Runtime.PyImport_GetModuleDict();
	    IntPtr op = Runtime.PyDict_GetItemString(dict, "__builtin__");

	    PyBaseObjectType = Runtime.PyObject_GetAttrString(op, "object"); 

	    PyModuleType = Runtime.PyObject_Type(op); 
	    PyNone = Runtime.PyObject_GetAttrString(op, "None"); 
	    PyTrue = Runtime.PyObject_GetAttrString(op, "True"); 
	    PyFalse = Runtime.PyObject_GetAttrString(op, "False"); 

	    PyBoolType = Runtime.PyObject_Type(PyTrue); 
	    PyNoneType = Runtime.PyObject_Type(PyNone); 
	    PyTypeType = Runtime.PyObject_Type(PyNoneType); 

	    op = Runtime.PyObject_GetAttrString(dict, "keys");
	    PyMethodType = Runtime.PyObject_Type(op);
	    Runtime.Decref(op);

	    op = Runtime.PyString_FromString("string");
	    PyStringType = Runtime.PyObject_Type(op); 
	    Runtime.Decref(op);

	    op = Runtime.PyUnicode_FromString("unicode");
	    PyUnicodeType = Runtime.PyObject_Type(op); 
	    Runtime.Decref(op);

	    op = Runtime.PyTuple_New(0);
	    PyTupleType = Runtime.PyObject_Type(op); 
	    Runtime.Decref(op);

	    op = Runtime.PyList_New(0);
	    PyListType = Runtime.PyObject_Type(op); 
	    Runtime.Decref(op);

	    op = Runtime.PyDict_New();
	    PyDictType = Runtime.PyObject_Type(op); 
	    Runtime.Decref(op);

	    op = Runtime.PyInt_FromInt32(0);
	    PyIntType = Runtime.PyObject_Type(op);  
	    Runtime.Decref(op);

	    op = Runtime.PyLong_FromLong(0);
	    PyLongType = Runtime.PyObject_Type(op); 
	    Runtime.Decref(op);

	    op = Runtime.PyFloat_FromDouble(0);
	    PyFloatType = Runtime.PyObject_Type(op); 
	    Runtime.Decref(op);

	    IntPtr s = Runtime.PyString_FromString("_temp");
	    IntPtr d = Runtime.PyDict_New();
	    IntPtr c = Runtime.PyClass_New(IntPtr.Zero, d, s);
	    PyClassType = Runtime.PyObject_Type(c);

	    IntPtr i = Runtime.PyInstance_New(c, IntPtr.Zero, IntPtr.Zero);
	    PyInstanceType = Runtime.PyObject_Type(i); 

	    Runtime.Decref(s);
	    Runtime.Decref(i);
	    Runtime.Decref(c);
	    Runtime.Decref(d);

	    Error = new IntPtr(-1);

	    // Determine whether we need to wrap exceptions for versions of
	    // of the Python runtime that do not allow new-style classes to
	    // be used as exceptions (Python versions 2.4 and lower).

	    IntPtr m = PyImport_ImportModule("exceptions");
	    op = Runtime.PyObject_GetAttrString(m, "Exception");
	    if (Runtime.PyObject_TYPE(op) == PyClassType) { 
		wrap_exceptions = true;
	    }
	    Runtime.Decref(op);
	    Runtime.Decref(m);

	    //wrap_exceptions = false;

	    // Initialize modules that depend on the runtime class.
  	    AssemblyManager.Initialize();
  	    PyCLRMetaType = MetaType.Initialize();
  	    Exceptions.Initialize();
  	    ImportHook.Initialize();

	    // Need to add the runtime directory to sys.path so that we
	    // can find built-in assemblies like System.Data, et. al.
	    string rtdir = RuntimeEnvironment.GetRuntimeDirectory();
	    IntPtr path = Runtime.PySys_GetObject("path");
	    IntPtr item = Runtime.PyString_FromString(rtdir);
	    Runtime.PyList_Append(path, item);
	    Runtime.Decref(item);
	    AssemblyManager.UpdatePath();
	}

	internal static void Shutdown() {
  	    AssemblyManager.Shutdown();
  	    Exceptions.Shutdown();
  	    ImportHook.Shutdown();
	    Py_Finalize();
	}

        internal static IntPtr Py_file_input = (IntPtr)257;

	internal static IntPtr PyBaseObjectType;
	internal static IntPtr PyModuleType;
	internal static IntPtr PyClassType;
	internal static IntPtr PyInstanceType;
	internal static IntPtr PyCLRMetaType;
	internal static IntPtr PyMethodType;

	internal static IntPtr PyUnicodeType;
	internal static IntPtr PyStringType;
	internal static IntPtr PyTupleType;
	internal static IntPtr PyListType;	
	internal static IntPtr PyDictType;	
	internal static IntPtr PyIntType;
	internal static IntPtr PyLongType;
	internal static IntPtr PyFloatType;
	internal static IntPtr PyBoolType;
	internal static IntPtr PyNoneType;
	internal static IntPtr PyTypeType;

	internal static IntPtr PyTrue;
	internal static IntPtr PyFalse;
	internal static IntPtr PyNone;
	internal static IntPtr Error;



	internal static IntPtr GetBoundArgTuple(IntPtr obj, IntPtr args) {
	    if (Runtime.PyObject_TYPE(args) != Runtime.PyTupleType) {
		Exceptions.SetError(Exceptions.TypeError, "tuple expected");
		return IntPtr.Zero;
	    }
	    int size = Runtime.PyTuple_Size(args);
	    IntPtr items = Runtime.PyTuple_New(size + 1);
	    Runtime.PyTuple_SetItem(items, 0, obj);
	    Runtime.Incref(obj);

	    for (int i = 0; i < size; i++) {
		IntPtr item = Runtime.PyTuple_GetItem(args, i);
		Runtime.Incref(item);
		Runtime.PyTuple_SetItem(items, i + 1, item);   
	    }

	    return items;
	}


	internal static IntPtr ExtendTuple(IntPtr t, params IntPtr[] args) {
	    int size = Runtime.PyTuple_Size(t);
	    int add = args.Length;
	    int total = size + add;
	    IntPtr item;

	    IntPtr items = Runtime.PyTuple_New(size + add);
	    for (int i = 0; i < size; i++) {
		item = Runtime.PyTuple_GetItem(t, i);
		Runtime.Incref(item);
		Runtime.PyTuple_SetItem(items, i, item);   
	    }

	    for (int n = 0; n < add; n++) {
		item = args[n];
		Runtime.Incref(item);
		Runtime.PyTuple_SetItem(items, size + n, item);   
	    }

	    return items;
	}

	internal static Type[] PythonArgsToTypeArray(IntPtr arg) {
	    // Given a PyObject * that is either a single type object or a
	    // tuple of (managed or unmanaged) type objects, return a Type[]
	    // containing the CLR Type objects that map to those types.
	    IntPtr args = arg;
	    bool free = false;

	    if (!Runtime.PyTuple_Check(arg)) {
		args = Runtime.PyTuple_New(1);
		Runtime.Incref(arg);
		Runtime.PyTuple_SetItem(args, 0, arg);
		free = true;
	    }

	    int n = Runtime.PyTuple_Size(args);
	    Type[] types = new Type[n];
	    Type t = null;

	    for (int i = 0; i < n; i++) {
		IntPtr op = Runtime.PyTuple_GetItem(args, i);
		ManagedType mt = ManagedType.GetManagedObject(op);

		if (mt is ClassBase) {
		    t = ((ClassBase)mt).type;
		}
		else if (mt is CLRObject) {
		    object inst = ((CLRObject)mt).inst;
		    if (inst is Type) {
			t = inst as Type;
		    }
		}
		else {
		    t = Converter.GetTypeByAlias(op);
		}

		if (t == null) {
		    types = null;
		    break;
		}
		types[i] = t;
	    }
	    if (free) {
		Runtime.Decref(args);
	    }
	    return types;
	}

	//===================================================================
	// Managed exports of the Python C API. Where appropriate, we do 
	// some optimization to avoid managed <--> unmanaged transitions
	// (mostly for heavily used methods).
	//===================================================================

	internal unsafe static void Incref(IntPtr op) {
	    void *p = (void *)op;
	    if ((void *)0 != p) {
		if (is32bit) { (*(int *)p)++;  }
		else         { (*(long *)p)++; }
	    }
	}

	internal unsafe static void Decref(IntPtr op) {
	    void *p = (void *)op;
	    if ((void *)0 != p) {
		if (is32bit) { --(*(int *)p);  }
		else         { --(*(long *)p); }
		if ((*(int *)p) == 0) {
		    void *t = is32bit ? (void *)(*((uint *)p + 1)) :
			                (void *)(*((ulong *)p + 1));
		    void *f = is32bit ? (void *)(*((uint *)t + 6)) :
			                (void *)(*((ulong *)t + 6));
		    if ((void *)0 == f) {
			return;
		    }
		    NativeCall.Impl.Void_Call_1(new IntPtr(f), op);
		    return;
		}
	    }
	}


	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	Py_Initialize();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	Py_IsInitialized();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	Py_Finalize();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	Py_NewInterpreter();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern void
	Py_EndInterpreter(IntPtr threadState);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyThreadState_New(IntPtr istate);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyThreadState_Get();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyThread_get_key_value(IntPtr key);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int 
	PyThread_get_thread_ident();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyThread_set_key_value(IntPtr key, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyThreadState_Swap(IntPtr key);


	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyGILState_Ensure();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PyGILState_Release(IntPtr gs);


	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyGILState_GetThisThreadState();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	public unsafe static extern int
	Py_Main(int argc, string[] argv);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PyEval_InitThreads();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PyEval_AcquireLock();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PyEval_ReleaseLock();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PyEval_AcquireThread(IntPtr tstate);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PyEval_ReleaseThread(IntPtr tstate);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyEval_SaveThread();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PyEval_RestoreThread(IntPtr tstate);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyEval_GetBuiltins();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyEval_GetGlobals();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyEval_GetLocals();


	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		    ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern string
        Py_GetProgramName();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern void
        Py_SetProgramName(string name);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern string
        Py_GetPythonHome();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern void
        Py_SetPythonHome(string home);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern string
        Py_GetVersion();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern string
        Py_GetPlatform();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern string
        Py_GetCopyright();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern string
        Py_GetCompiler();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern string
        Py_GetBuildInfo();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern int
        PyRun_SimpleString(string code);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern IntPtr
        PyRun_String(string code, IntPtr st, IntPtr globals, IntPtr locals);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern IntPtr
        Py_CompileString(string code, string file, IntPtr tok);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern IntPtr
        PyImport_ExecCodeModule(string name, IntPtr code);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyCFunction_New(IntPtr ml, IntPtr self);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyCFunction_NewEx(IntPtr ml, IntPtr self, IntPtr mod);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyCFunction_Call(IntPtr func, IntPtr args, IntPtr kw);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyClass_New(IntPtr bases, IntPtr dict, IntPtr name);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyInstance_New(IntPtr cls, IntPtr args, IntPtr kw);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyInstance_NewRaw(IntPtr cls, IntPtr dict);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyMethod_New(IntPtr func, IntPtr self, IntPtr cls);


	//====================================================================
	// Python abstract object API
	//====================================================================

	// A macro-like method to get the type of a Python object. This is
	// designed to be lean and mean in IL & avoid managed <-> unmanaged 
	// transitions. Note that this does not incref the type object.

	internal unsafe static IntPtr 
	PyObject_TYPE(IntPtr op) {
	    void *p = (void *)op;
	    if ((void *)0 == p) {
		return IntPtr.Zero;
	    }
	    if (is32bit) {
		return new IntPtr((void *)(*((uint *)p + 1)));
	    }
	    else {
		return new IntPtr((void *)(*((ulong *)p + 1)));
	    }
	}

	// Managed version of the standard Python C API PyObject_Type call.
	// This version avoids a managed <-> unmanaged transition. This one
	// does incref the returned type object.

	internal unsafe static IntPtr 
	PyObject_Type(IntPtr op) {
	    IntPtr tp = PyObject_TYPE(op);
	    Runtime.Incref(tp);
	    return tp;
	}

	internal static string PyObject_GetTypeName(IntPtr op) {
	    IntPtr pyType = Marshal.ReadIntPtr(op, IntPtr.Size);
	    IntPtr ppName = Marshal.ReadIntPtr(pyType, (3 * IntPtr.Size));
	    return Marshal.PtrToStringAnsi(ppName);
	}

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern int
        PyObject_HasAttrString(IntPtr pointer, string name);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern IntPtr
        PyObject_GetAttrString(IntPtr pointer, string name);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern int
        PyObject_SetAttrString(IntPtr pointer, string name, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern int
        PyObject_HasAttr(IntPtr pointer, IntPtr name);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern IntPtr
        PyObject_GetAttr(IntPtr pointer, IntPtr name);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern int
        PyObject_SetAttr(IntPtr pointer, IntPtr name, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern IntPtr
        PyObject_GetItem(IntPtr pointer, IntPtr key);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern int
        PyObject_SetItem(IntPtr pointer, IntPtr key, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern int
        PyObject_DelItem(IntPtr pointer, IntPtr key);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern IntPtr
        PyObject_GetIter(IntPtr op);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern IntPtr
        PyObject_Call(IntPtr pointer, IntPtr args, IntPtr kw);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyObject_CallObject(IntPtr pointer, IntPtr args);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyObject_Compare(IntPtr value1, IntPtr value2);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyObject_IsInstance(IntPtr ob, IntPtr type);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyObject_IsSubclass(IntPtr ob, IntPtr type);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyCallable_Check(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyObject_IsTrue(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyObject_Size(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyObject_Hash(IntPtr op);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyObject_Repr(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyObject_Str(IntPtr pointer);


	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyObject_Dir(IntPtr pointer);


	//====================================================================
	// Python number API
	//====================================================================

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyNumber_Int(IntPtr ob);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyNumber_Long(IntPtr ob);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyNumber_Float(IntPtr ob);


	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern bool
	PyNumber_Check(IntPtr ob);


	internal static bool PyInt_Check(IntPtr ob) {
	    return PyObject_TypeCheck(ob, Runtime.PyIntType);
	}

	internal static bool PyBool_Check(IntPtr ob) {
	    return PyObject_TypeCheck(ob, Runtime.PyBoolType);
	}



	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	private unsafe static extern IntPtr
	PyInt_FromLong(IntPtr value);

	internal static IntPtr PyInt_FromInt32(int value) {
	    IntPtr v = new IntPtr(value);
	    return PyInt_FromLong(v);
	}

	internal static IntPtr PyInt_FromInt64(long value) {
	    IntPtr v = new IntPtr(value);
	    return PyInt_FromLong(v);
	}


	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyInt_AsLong(IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyInt_FromString(string value, IntPtr end, int radix);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyInt_GetMax();


	internal static bool PyLong_Check(IntPtr ob) {
	    return PyObject_TYPE(ob) == Runtime.PyLongType;
	}

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyLong_FromLong(long value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyLong_FromUnsignedLong(uint value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyLong_FromDouble(double value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyLong_FromLongLong(long value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyLong_FromUnsignedLongLong(ulong value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyLong_FromString(string value, IntPtr end, int radix);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyLong_AsLong(IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern uint
	PyLong_AsUnsignedLong(IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern long
	PyLong_AsLongLong(IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern ulong
	PyLong_AsUnsignedLongLong(IntPtr value);


	internal static bool PyFloat_Check(IntPtr ob) {
	    return PyObject_TYPE(ob) == Runtime.PyFloatType;
	}

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyFloat_FromDouble(double value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyFloat_FromString(IntPtr value, IntPtr junk);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern double
	PyFloat_AsDouble(IntPtr ob);


	//====================================================================
	// Python sequence API
	//====================================================================

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern bool
	PySequence_Check(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PySequence_GetItem(IntPtr pointer, int index);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PySequence_SetItem(IntPtr pointer, int index, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PySequence_DelItem(IntPtr pointer, int index);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PySequence_GetSlice(IntPtr pointer, int i1, int i2);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PySequence_SetSlice(IntPtr pointer, int i1, int i2, IntPtr v);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PySequence_DelSlice(IntPtr pointer, int i1, int i2);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PySequence_Size(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PySequence_Contains(IntPtr pointer, IntPtr item);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PySequence_Concat(IntPtr pointer, IntPtr other);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PySequence_Repeat(IntPtr pointer, int count);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PySequence_Index(IntPtr pointer, IntPtr item);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PySequence_Count(IntPtr pointer, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PySequence_Tuple(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PySequence_List(IntPtr pointer);


	//====================================================================
	// Python string API
	//====================================================================

	internal static bool IsStringType(IntPtr op) {
	    IntPtr t = PyObject_TYPE(op);
	    return (t == PyStringType) || (t == PyUnicodeType);
	}

	internal static bool PyString_Check(IntPtr ob) {
	    return PyObject_TYPE(ob) == Runtime.PyStringType;
	}

	internal static IntPtr PyString_FromString(string value) {
	    return PyString_FromStringAndSize(value, value.Length);
	}

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyString_FromStringAndSize(string value, int size);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   EntryPoint="PyString_AsString",
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr 
	PyString_AS_STRING(IntPtr op);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyString_Size(IntPtr pointer);


	internal static bool PyUnicode_Check(IntPtr ob) {
	    return PyObject_TYPE(ob) == Runtime.PyUnicodeType;
	}

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   EntryPoint="PyUnicodeUCS2_FromObject",
		   ExactSpelling=true, CharSet=CharSet.Unicode)]
	internal unsafe static extern IntPtr
	PyUnicode_FromObject(IntPtr ob);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   EntryPoint="PyUnicodeUCS2_FromEncodedObject",
		   ExactSpelling=true, CharSet=CharSet.Unicode)]
	internal unsafe static extern IntPtr
	PyUnicode_FromEncodedObject(IntPtr ob, IntPtr enc, IntPtr err);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   EntryPoint="PyUnicodeUCS2_FromUnicode",
		   ExactSpelling=true, CharSet=CharSet.Unicode)]
	internal unsafe static extern IntPtr
	PyUnicode_FromUnicode(string s, int size);

	internal static IntPtr PyUnicode_FromString(string s) {
	    return PyUnicode_FromUnicode(s, (s.Length));
	}

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   EntryPoint="PyUnicodeUCS2_GetSize",
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyUnicode_GetSize(IntPtr ob);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   EntryPoint="PyUnicodeUCS2_AsUnicode",
		   ExactSpelling=true)]
	internal unsafe static extern char *
	PyUnicode_AsUnicode(IntPtr ob);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   EntryPoint="PyUnicodeUCS2_AsUnicode",
		   ExactSpelling=true, CharSet=CharSet.Unicode)]
	internal unsafe static extern IntPtr
	PyUnicode_AS_UNICODE(IntPtr op);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   EntryPoint="PyUnicodeUCS2_FromOrdinal",
		   ExactSpelling=true, CharSet=CharSet.Unicode)]
	internal unsafe static extern IntPtr 
	PyUnicode_FromOrdinal(int c);

	internal unsafe static string GetManagedString(IntPtr op) {
	    IntPtr type = PyObject_TYPE(op);

	    if (type == Runtime.PyStringType) {
		return Marshal.PtrToStringAnsi(
			       PyString_AS_STRING(op), 
			       Runtime.PyString_Size(op)
			       );
	    }

	    if (type == Runtime.PyUnicodeType) {
		char *p = Runtime.PyUnicode_AsUnicode(op);
		int size = Runtime.PyUnicode_GetSize(op);
		return new String(p, 0, size);
	    }

	    return null;
	}


	//====================================================================
	// Python dictionary API
	//====================================================================

	internal static bool PyDict_Check(IntPtr ob) {
	    return PyObject_TYPE(ob) == Runtime.PyDictType;
	}

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyDict_New();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyDictProxy_New(IntPtr dict);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyDict_GetItem(IntPtr pointer, IntPtr key);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyDict_GetItemString(IntPtr pointer, string key);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyDict_SetItem(IntPtr pointer, IntPtr key, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyDict_SetItemString(IntPtr pointer, string key, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyDict_DelItem(IntPtr pointer, IntPtr key);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyMapping_HasKey(IntPtr pointer, IntPtr key);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyDict_Keys(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyDict_Values(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyDict_Items(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyDict_Copy(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyDict_Update(IntPtr pointer, IntPtr other);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PyDict_Clear(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyDict_Size(IntPtr pointer);


	//====================================================================
	// Python list API
	//====================================================================

	internal static bool PyList_Check(IntPtr ob) {
	    return PyObject_TYPE(ob) == Runtime.PyListType;
	}

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyList_New(int size);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyList_AsTuple(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyList_GetItem(IntPtr pointer, int index);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyList_SetItem(IntPtr pointer, int index, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyList_Insert(IntPtr pointer, int index, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyList_Append(IntPtr pointer, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyList_Reverse(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyList_Sort(IntPtr pointer);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyList_GetSlice(IntPtr pointer, int start, int end);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyList_SetSlice(IntPtr pointer, int start, int end, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyList_Size(IntPtr pointer);


	//====================================================================
	// Python tuple API
	//====================================================================

	internal static bool PyTuple_Check(IntPtr ob) {
	    return PyObject_TYPE(ob) == Runtime.PyTupleType;
	}

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyTuple_New(int size);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyTuple_GetItem(IntPtr pointer, int index);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyTuple_SetItem(IntPtr pointer, int index, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyTuple_GetSlice(IntPtr pointer, int start, int end);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyTuple_Size(IntPtr pointer);


	//====================================================================
	// Python module API
	//====================================================================

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern string
	PyModule_GetName(IntPtr module);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyModule_GetDict(IntPtr module);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern string
	PyModule_GetFilename(IntPtr module);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyImport_Import(IntPtr name);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyImport_ImportModule(string name);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyImport_ReloadModule(IntPtr module);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyImport_AddModule(string name);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyImport_GetModuleDict();


	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PySys_SetArgv(int argc, IntPtr argv);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PySys_GetObject(string name);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PySys_SetObject(string name, IntPtr ob);


	//====================================================================
	// Python type object API
	//====================================================================

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern bool 
	PyType_IsSubtype(IntPtr t1, IntPtr t2);

        internal static bool PyObject_TypeCheck(IntPtr ob, IntPtr tp) {
	  IntPtr t = PyObject_TYPE(ob);
	  return (t == tp) || PyType_IsSubtype(t, tp);
	}

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyType_GenericNew(IntPtr type, IntPtr args, IntPtr kw);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyType_GenericAlloc(IntPtr type, int n);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyType_Ready(IntPtr type);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	_PyType_Lookup(IntPtr type, IntPtr name);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyObject_GenericGetAttr(IntPtr obj, IntPtr name);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyObject_GenericSetAttr(IntPtr obj, IntPtr name, IntPtr value);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	_PyObject_GetDictPtr(IntPtr obj);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyObject_GC_New(IntPtr tp);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void 
	PyObject_GC_Del(IntPtr tp);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void 
	PyObject_GC_Track(IntPtr tp);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void 
	PyObject_GC_UnTrack(IntPtr tp);


	//====================================================================
	// Python memory API
	//====================================================================

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyMem_Malloc(int size);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyMem_Realloc(IntPtr ptr, int size);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PyMem_Free(IntPtr ptr);


	//====================================================================
	// Python exception API
	//====================================================================

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PyErr_SetString(IntPtr ob, string message);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PyErr_SetObject(IntPtr ob, IntPtr message);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern IntPtr
	PyErr_SetFromErrno(IntPtr ob);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern void
	PyErr_SetNone(IntPtr ob);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyErr_ExceptionMatches(IntPtr exception);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyErr_GivenExceptionMatches(IntPtr ob, IntPtr val);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern void
	PyErr_NormalizeException(IntPtr ob, IntPtr val, IntPtr tb);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
	internal unsafe static extern int
	PyErr_Occurred();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern void
	PyErr_Fetch(ref IntPtr ob, ref IntPtr val, ref IntPtr tb);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern void
	PyErr_Restore(IntPtr ob, IntPtr val, IntPtr tb);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern void
	PyErr_Clear();

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern void
	PyErr_Print();


	//====================================================================
	// Miscellaneous
	//====================================================================

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern IntPtr
	PyMethod_Self(IntPtr ob);

	[DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
		   ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern IntPtr
	PyMethod_Function(IntPtr ob);

    }


}
