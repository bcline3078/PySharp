// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Reflection;
using System.Collections;
using System.Runtime.InteropServices;


namespace Python.Runtime {

    /// <summary>
    /// Base class for Python types that reflect managed exceptions based on
    /// System.Exception
    /// </summary>
    /// <remarks>
    /// The Python wrapper for managed exceptions LIES about its inheritance
    /// tree. Although the real System.Exception is a subclass of 
    /// System.Object the Python type for System.Exception does NOT claim that
    /// it subclasses System.Object. Instead TypeManager.CreateType() uses 
    /// Python's exception.Exception class as base class for System.Exception.
    /// </remarks>
    internal class ExceptionClassObject : ClassObject {

        internal new bool is_exception {
            get { return true; }
        }

        internal ExceptionClassObject(Type tp) : base(tp) {}
    }

    /// <summary>
    /// Encapsulates the Python exception APIs.
    /// </summary>

    public class Exceptions {

	private Exceptions() {}

	//===================================================================
	// Initialization performed on startup of the Python runtime.
	//===================================================================

	internal static void Initialize() {
	    IntPtr module = Runtime.PyImport_ImportModule("exceptions");
            Exceptions.ErrorCheck(module);
	    Type type = typeof(Exceptions);
	    foreach (FieldInfo fi in type.GetFields(BindingFlags.Public | 
						    BindingFlags.Static)) {
		IntPtr op = Runtime.PyObject_GetAttrString(module, fi.Name);
                if (op != IntPtr.Zero) {
                    fi.SetValue(type, op);
                }
                else {
                    DebugUtil.Print("Unknown exception: " + fi.Name);
                }
	    }
	    Runtime.Decref(module);
	    Runtime.PyErr_Clear();
	    if (Runtime.wrap_exceptions) {
		SetupExceptionHack();
	    }
	}


	//===================================================================
	// Cleanup resources upon shutdown of the Python runtime.
	//===================================================================

	internal static void Shutdown() {
	    Type type = typeof(Exceptions);
	    foreach (FieldInfo fi in type.GetFields(BindingFlags.Public | 
						    BindingFlags.Static)) {
		IntPtr op = (IntPtr)fi.GetValue(type);
		Runtime.Decref(op);
	    }
	}

        /// <summary>
        ///  Shortcut for (pointer == NULL) -> throw PythonException
        /// </summary>
        /// <param name="pointer">Pointer to a Python object</param>
        internal unsafe static void ErrorCheck(IntPtr pointer) {
            if (pointer == IntPtr.Zero) {
                throw new PythonException();
            }
        }

        /// <summary>
        ///  Shortcut for (pointer == NULL or ErrorOccurred()) -> throw PythonException
        /// </summary>
        ///  Shortcut for (pointer == NULL) -> throw PythonException
        internal unsafe static void ErrorOccurredCheck(IntPtr pointer) {
            if ((pointer == IntPtr.Zero) || Exceptions.ErrorOccurred()) {
                throw new PythonException();
            }
        }

	// Versions of CPython up to 2.4 do not allow exceptions to be
	// new-style classes. To get around that restriction and provide
	// a consistent user experience for programmers, we wrap managed
	// exceptions in an old-style class that (through some dont-try-
	// this-at-home hackery) delegates to the managed exception and
	// obeys the conventions of both Python and managed exceptions.

	static IntPtr ns_exc; // new-style class for System.Exception
	static IntPtr os_exc; // old-style class for System.Exception
	static Hashtable cache;

	internal static void SetupExceptionHack() {
	    ns_exc = ClassManager.GetClass(typeof(Exception)).pyHandle;
	    cache = new Hashtable();

	    string code = 
            "import exceptions\n" +
            "class Exception(exceptions.Exception):\n" +
            "    _class = None\n" +
	    "    _inner = None\n" +
            "\n" +
            "    def __init__(self, *args, **kw):\n" +
            "        inst = self.__class__._class(*args, **kw)\n" +
            "        self.__dict__['_inner'] = inst\n" +
            "        exceptions.Exception.__init__(self, *args, **kw)\n" +
            "\n" +
            "    def __getattr__(self, name, _marker=[]):\n" +
            "        inner = self.__dict__['_inner']\n" +
            "        v = getattr(inner, name, _marker)\n" +
            "        if v is not _marker:\n" +
            "            return v\n" +
            "        v = self.__dict__.get(name, _marker)\n" +
            "        if v is not _marker:\n" +
            "            return v\n" +
            "        raise AttributeError(name)\n" +
            "\n" +
            "    def __setattr__(self, name, value):\n" +
            "        inner = self.__dict__['_inner']\n" +
            "        setattr(inner, name, value)\n" +
            "\n" +
            "    def __str__(self):\n" +
            "        inner = self.__dict__.get('_inner')\n" +
            "        msg = getattr(inner, 'Message', '')\n" +
            "        st = getattr(inner, 'StackTrace', '')\n" +
            "        st = st and '\\n' + st or ''\n" +
            "        return msg + st\n" +
            "\n";

	    IntPtr dict = Runtime.PyDict_New();

	    IntPtr builtins = Runtime.PyEval_GetBuiltins();
	    Runtime.PyDict_SetItemString(dict, "__builtins__", builtins);

	    IntPtr namestr = Runtime.PyString_FromString("System");
	    Runtime.PyDict_SetItemString(dict, "__name__", namestr);
	    Runtime.Decref(namestr);

	    Runtime.PyDict_SetItemString(dict, "__file__", Runtime.PyNone);
	    Runtime.PyDict_SetItemString(dict, "__doc__", Runtime.PyNone);

	    IntPtr flag = Runtime.Py_file_input;
	    IntPtr done = Runtime.PyRun_String(code, flag, dict, dict);

	    os_exc = Runtime.PyDict_GetItemString(dict, "Exception");
	    Runtime.PyObject_SetAttrString(os_exc, "_class", ns_exc);
	    Runtime.PyErr_Clear();
	}


	internal static IntPtr GenerateExceptionClass(IntPtr real) {
	    if (real == ns_exc) {
                return os_exc;
	    }

	    IntPtr nbases = Runtime.PyObject_GetAttrString(real, "__bases__");
	    if (Runtime.PyTuple_Size(nbases) != 1) {
		throw new SystemException("Invalid __bases__");
	    }
	    IntPtr nsbase = Runtime.PyTuple_GetItem(nbases, 0);
	    Runtime.Decref(nbases);

	    IntPtr osbase = GetExceptionClassWrapper(nsbase);
            IntPtr baselist = Runtime.PyTuple_New(1);
	    Runtime.Incref(osbase);
	    Runtime.PyTuple_SetItem(baselist, 0, osbase);
	    IntPtr name = Runtime.PyObject_GetAttrString(real, "__name__");

	    IntPtr dict = Runtime.PyDict_New();
	    IntPtr mod = Runtime.PyObject_GetAttrString(real, "__module__");
	    Runtime.PyDict_SetItemString(dict, "__module__", mod);
	    Runtime.Decref(mod);

	    IntPtr subc = Runtime.PyClass_New(baselist, dict, name);
	    Runtime.Decref(baselist);
	    Runtime.Decref(dict);
	    Runtime.Decref(name);

	    Runtime.PyObject_SetAttrString(subc, "_class", real);
	    return subc;
	}

	internal static IntPtr GetExceptionClassWrapper(IntPtr real) {
	    // Given the pointer to a new-style class representing a managed
	    // exception, return an appropriate old-style class wrapper that
	    // maintains all of the expectations and delegates to the wrapped
	    // class.
	    object ob = cache[real];
	    if (ob == null) {
		IntPtr op = GenerateExceptionClass(real);
		cache[real] = op;
		return op;
	    }
	    return (IntPtr)ob;
	}

	internal static IntPtr GetExceptionInstanceWrapper(IntPtr real) {
	    // Given the pointer to a new-style class instance representing a 
	    // managed exception, return an appropriate old-style class 
	    // wrapper instance that delegates to the wrapped instance.
	    IntPtr tp = Runtime.PyObject_TYPE(real);
	    if (Runtime.PyObject_TYPE(tp) == Runtime.PyInstanceType) {
		return real;
	    }
	    // Get / generate a class wrapper, instantiate it and set its
	    // _inner attribute to the real new-style exception instance.
	    IntPtr ct = GetExceptionClassWrapper(tp);
            Exceptions.ErrorCheck(ct);
	    IntPtr op = Runtime.PyInstance_NewRaw(ct, IntPtr.Zero);
            Exceptions.ErrorCheck(op);
	    IntPtr d = Runtime.PyObject_GetAttrString(op, "__dict__");
            Exceptions.ErrorCheck(d);
	    Runtime.PyDict_SetItemString(d, "_inner", real);
	    Runtime.Decref(d);
	    return op;
	}

	internal static IntPtr UnwrapExceptionClass(IntPtr op) {
	    // In some cases its necessary to recognize an exception *class*,
	    // and obtain the inner (wrapped) exception class. This method
	    // returns the inner class if found, or a null pointer.

	    IntPtr d = Runtime.PyObject_GetAttrString(op, "__dict__");
	    if (d == IntPtr.Zero) {
		Exceptions.Clear();
		return IntPtr.Zero;
	    }
	    IntPtr c = Runtime.PyDict_GetItemString(d, "_class");
	    Runtime.Decref(d);
	    if (c == IntPtr.Zero) {
		Exceptions.Clear();
	    }
	    return c;
	}


	/// <summary>
	/// GetException Method
	/// </summary>
	///
	/// <remarks>
	/// Retrieve Python exception information as a PythonException
	/// instance. The properties of the PythonException may be used
	/// to access the exception type, value and traceback info.
	/// </remarks>

	public static PythonException GetException() {
	    // TODO: implement this.
	    return null;
	}

	/// <summary>
	/// ExceptionMatches Method
	/// </summary>
	///
	/// <remarks>
	/// Returns true if the current Python exception matches the given
	/// Python object. This is a wrapper for PyErr_ExceptionMatches.
	/// </remarks>

	public static bool ExceptionMatches(IntPtr ob) {
	    return Runtime.PyErr_ExceptionMatches(ob) != 0;
	}

	/// <summary>
	/// ExceptionMatches Method
	/// </summary>
	///
	/// <remarks>
	/// Returns true if the given Python exception matches the given
	/// Python object. This is a wrapper for PyErr_GivenExceptionMatches.
	/// </remarks>

	public static bool ExceptionMatches(IntPtr exc, IntPtr ob) {
	    int i = Runtime.PyErr_GivenExceptionMatches(exc, ob);
	    return (i != 0);
	}

	/// <summary>
	/// SetError Method
	/// </summary>
	///
	/// <remarks>
	/// Sets the current Python exception given a native string.
	/// This is a wrapper for the Python PyErr_SetString call.
	/// </remarks>

	public static void SetError(IntPtr ob, string value) {
	    Runtime.PyErr_SetString(ob, value);
	}

	/// <summary>
	/// SetError Method
	/// </summary>
	///
	/// <remarks>
	/// Sets the current Python exception given a Python object.
	/// This is a wrapper for the Python PyErr_SetObject call.
	/// </remarks>

	public static void SetError(IntPtr ob, IntPtr value) {
	    Runtime.PyErr_SetObject(ob, value);
	}

	/// <summary>
	/// SetError Method
	/// </summary>
	///
	/// <remarks>
	/// Sets the current Python exception given a CLR exception
	/// object. The CLR exception instance is wrapped as a Python
	/// object, allowing it to be handled naturally from Python.
	/// </remarks>

	public static void SetError(Exception e) {

	    // Because delegates allow arbitrary nestings of Python calling
	    // managed calling Python calling... etc. it is possible that we
	    // might get a managed exception raised that is a wrapper for a
	    // Python exception. In that case we'd rather have the real thing.

	    PythonException pe = e as PythonException;
	    if (pe != null) {
		Runtime.PyErr_SetObject(pe.PyType, pe.PyValue);
		return;
	    }

	    IntPtr op = CLRObject.GetInstHandle(e);

	    // XXX - hack to raise a compatible old-style exception ;(
	    if (Runtime.wrap_exceptions) {
		op = GetExceptionInstanceWrapper(op);
	    }
	    IntPtr etype = Runtime.PyObject_GetAttrString(op, "__class__");
	    Runtime.PyErr_SetObject(etype, op);
	    Runtime.Decref(etype);
	}

	/// <summary>
	/// ErrorOccurred Method
	/// </summary>
	///
	/// <remarks>
	/// Returns true if an exception occurred in the Python runtime.
	/// This is a wrapper for the Python PyErr_Occurred call.
	/// </remarks>

	public static bool ErrorOccurred() {
	    return Runtime.PyErr_Occurred() != 0;
	}

	/// <summary>
	/// Clear Method
	/// </summary>
	///
	/// <remarks>
	/// Clear any exception that has been set in the Python runtime.
	/// </remarks>

	public static void Clear() {
	    Runtime.PyErr_Clear();
	}

        //====================================================================
        // helper methods for raising warnings
        //====================================================================

        /// <summary>
        /// XXX This method is buggy! It causes an access violation.
        /// </summary>
        public static void warn(string message, string exceptionName, int stacklevel)
        {
            return; // XXX
            IntPtr excMod = Runtime.PyImport_ImportModule("exceptions");
            Exceptions.ErrorCheck(excMod);
            IntPtr excClass = Runtime.PyObject_GetAttrString(excMod, exceptionName);
            Exceptions.ErrorCheck(excClass);
            Runtime.Decref(excMod);

            IntPtr warningsMod = Runtime.PyImport_ImportModule("warnings");
            Exceptions.ErrorCheck(warningsMod);
            IntPtr warnFunc = Runtime.PyObject_GetAttrString(warningsMod, "warn");
            Exceptions.ErrorCheck(warnFunc);
            Runtime.Decref(warningsMod);

            IntPtr args = Runtime.PyTuple_New(3);
            Exceptions.ErrorCheck(args);
            Runtime.PyTuple_SetItem(args, 0, Runtime.PyString_FromString(message));
            Runtime.PyTuple_SetItem(args, 1, excClass);
            Runtime.PyTuple_SetItem(args, 2, Runtime.PyInt_FromInt32(stacklevel));
            IntPtr result = Runtime.PyObject_Call(warnFunc, args, IntPtr.Zero);
            Exceptions.ErrorCheck(result);
            Runtime.Decref(warnFunc);
            Runtime.Decref(excClass);
            Runtime.Decref(args);
            Runtime.Decref(result);
        }

        public static void warn(string message, string exceptionName)
        {
            warn(message, exceptionName, 1);
        }

        public static void deprecation(string message, int stacklevel)
        {
            warn(message, "DeprecationWarning", stacklevel);
        }

        public static void deprecation(string message)
        {
            deprecation(message, 1);
        }

	//====================================================================
	// Internal helper methods for common error handling scenarios.
	//====================================================================

	internal static IntPtr RaiseTypeError(string message) {
	    Exceptions.SetError(Exceptions.TypeError, message);
	    return IntPtr.Zero;
	}


	public static IntPtr ArithmeticError;
	public static IntPtr AssertionError;
	public static IntPtr AttributeError;
#if (PYTHON25 || PYTHON26)
        public static IntPtr BaseException;
#endif
	public static IntPtr DeprecationWarning;
	public static IntPtr EOFError;
	public static IntPtr EnvironmentError;
	public static IntPtr Exception;
	public static IntPtr FloatingPointError;
        public static IntPtr FutureWarning;
        public static IntPtr GeneratorExit;
	public static IntPtr IOError;
	public static IntPtr ImportError;
        public static IntPtr ImportWarning;
	public static IntPtr IndentationError;
	public static IntPtr IndexError;
	public static IntPtr KeyError;
	public static IntPtr KeyboardInterrupt;
	public static IntPtr LookupError;
	public static IntPtr MemoryError;
	public static IntPtr NameError;
	public static IntPtr NotImplementedError;
	public static IntPtr OSError;
	public static IntPtr OverflowError;
        //public static IntPtr OverflowWarning;
        public static IntPtr PendingDeprecationWarning;
	public static IntPtr ReferenceError;
	public static IntPtr RuntimeError;
	public static IntPtr RuntimeWarning;
	public static IntPtr StandardError;
	public static IntPtr StopIteration;
	public static IntPtr SyntaxError;
	public static IntPtr SyntaxWarning;
	public static IntPtr SystemError;
	public static IntPtr SystemExit;
	public static IntPtr TabError;
	public static IntPtr TypeError;
	public static IntPtr UnboundLocalError;
        public static IntPtr UnicodeDecodeError;
        public static IntPtr UnicodeEncodeError;
	public static IntPtr UnicodeError;
        public static IntPtr UnicodeTranslateError;
        public static IntPtr UnicodeWarning;
	public static IntPtr UserWarning;
	public static IntPtr ValueError;
	public static IntPtr Warning;
	public static IntPtr WindowsError;
	public static IntPtr ZeroDivisionError;

    }


}
