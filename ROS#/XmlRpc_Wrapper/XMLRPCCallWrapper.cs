﻿#region USINGZ
//#define REFDEBUGWrapper
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
#endregion

namespace XmlRpc_Wrapper
{
    public class XMLRPCCallWrapper : IDisposable
    {
        

        #region Reference Tracking + unmanaged pointer management
        
        private IntPtr __instance;

        public void Dispose()
        {
            if (__instance != IntPtr.Zero && FUNC != null)
                FUNC = null;
        }

        ~XMLRPCCallWrapper()
        {
            Dispose();
        }

        public bool Initialized
        {
            get
            {
                return __instance != IntPtr.Zero;
            }
        }

        private static Dictionary<IntPtr, int> _refs = new Dictionary<IntPtr, int>();
        private static object reflock = new object();
#if REFDEBUGWrapper
        private static Thread refdumper;
        private static void dumprefs()
        {
            while (true)
            {
                Dictionary<IntPtr, int> dainbrammage = null;
                lock (reflock)
                {
                    dainbrammage = new Dictionary<IntPtr, int>(_refs);
                }
                Console.WriteLine("REF DUMP");
                foreach (KeyValuePair<IntPtr, int> reff in dainbrammage)
                {
                    Console.WriteLine("\t" + reff.Key + " = " + reff.Value);
                }
                Thread.Sleep(500);
            }
        }
#endif

        public static XMLRPCCallWrapper LookUp(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
            {
                AddRef(ptr);
                return new XMLRPCCallWrapper(ptr);
            }
            return null;
        }


        private static void AddRef(IntPtr ptr)
        {
#if REFDEBUGWrapper
            if (refdumper == null)
            {
                refdumper = new Thread(dumprefs);
                refdumper.IsBackground = true;
                refdumper.Start();
            }
#endif
            lock (reflock)
            {
                if (!_refs.ContainsKey(ptr))
                {
#if REFDEBUGWrapper
                    Console.WriteLine("Adding a new reference to: " + ptr + " (" + 0 + "==> " + 1 + ")");
#endif
                    _refs.Add(ptr, 1);
                }
                else
                {
#if REFDEBUGWrapper
                    Console.WriteLine("Adding a new reference to: " + ptr + " (" + _refs[ptr] + "==> " + (_refs[ptr] + 1) + ")");
#endif
                    _refs[ptr]++;
                }
            }
        }

        private static void RmRef(ref IntPtr ptr)
        {
            lock (reflock)
            {
                if (_refs.ContainsKey(ptr))
                {
#if REFDEBUGWrapper
                    Console.WriteLine("Removing a reference to: " + ptr + " (" + _refs[ptr] + "==> " + (_refs[ptr] - 1) + ")");
#endif
                    _refs[ptr]--;
                    if (_refs[ptr] <= 0)
                    {
#if REFDEBUGWrapper
                        Console.WriteLine("KILLING " + ptr + " BECAUSE IT'S A BITCH!");
#endif
                        _refs.Remove(ptr);
                        new XMLRPCCallWrapper(ptr).FUNC = null;
                        ptr = IntPtr.Zero;
                    }
                    return;
                }
            }
        }

        public IntPtr instance
        {
            get
            {
                return __instance;
            }
            set
            {
                if (value != IntPtr.Zero)
                {
                    if (__instance != IntPtr.Zero)
                        RmRef(ref __instance);
                    AddRef(value);
                    __instance = value;
                }
            }
        }

        #endregion

        private XMLRPCFunc _FUNC;

        public string name;
        public XmlRpcServer server;

        public XMLRPCCallWrapper(string function_name, XMLRPCFunc func, XmlRpcServer server)
        {
            name = function_name;
            this.server = server;
            __instance = create(function_name, server.instance);
            AddRef(__instance);
            SegFault();
            FUNC = func;
        }

        public XMLRPCCallWrapper(IntPtr ptr)
        {
            instance = ptr;
        }

        public XMLRPCFunc FUNC
        {
            get { return _FUNC; }
            set { SetFunc((_FUNC = value)); }
        }

        #region IDisposable Members

        #endregion
        
        public void SetFunc(XMLRPCFunc func)
        {
            SegFault();
            setfunc(instance, func);
        }

        public void Execute(XmlRpcValue parms, out XmlRpcValue reseseses)
        {
            SegFault();
            reseseses = new XmlRpcValue();
            execute(instance, parms.instance, reseseses.instance);
        }

        public void SegFault()
        {
            if (instance == IntPtr.Zero)
                throw new Exception("This isn't really a segfault, but your pointer is invalid, so it would have been!");
        }

        #region P/Invoke

        [DllImport("XmlRpcWin32.dll", EntryPoint = "XmlRpcServerMethod_Create", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr create([In] [Out] [MarshalAs(UnmanagedType.LPStr)] string name, IntPtr server);

        [DllImport("XmlRpcWin32.dll", EntryPoint = "XmlRpcServerMethod_SetFunc", CallingConvention = CallingConvention.Cdecl)]
        private static extern void setfunc(IntPtr target, [MarshalAs(UnmanagedType.FunctionPtr)] XMLRPCFunc cb);

        [DllImport("XmlRpcWin32.dll", EntryPoint = "XmlRpcServerMethod_Execute", CallingConvention = CallingConvention.Cdecl)]
        private static extern void execute(IntPtr target, [In] [Out] IntPtr parms, [In] [Out] IntPtr res);

        #endregion
    }


    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void XMLRPCFunc([In] [Out] IntPtr addrofparams, [In] [Out] IntPtr addrofresult);
}