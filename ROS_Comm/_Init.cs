using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Messages;
using Uml.Robotics.XmlRpc;
using m = Messages.std_msgs;
using gm = Messages.geometry_msgs;
using nm = Messages.nav_msgs;

namespace Uml.Robotics.Ros
{
    /// <summary>
    ///     Helper class for display and/or other output of debugging/peripheral information
    /// </summary>
    public static class EDB
    {
        #region Delegates

        /// <summary>
        ///     This delegate and associated event can be used for logging of all output from EDB to something controlled by the
        ///     program using ROS_Sharp, such as file, or other
        /// </summary>
        public delegate void otheroutput(object o);

        #endregion

        public static event otheroutput OtherOutput;

        private static bool toDebugInstead = false;
        private static bool toDebugInitialized = false;

        //does the actual writing
        private static void _writeline(object o)
        {
            if (OtherOutput != null)
                OtherOutput(o);

            if (!toDebugInitialized)
            {
                try
                {
                    if (Console.CursorVisible)
                    {
                        toDebugInstead = false;
                    }
                }
                catch (System.IO.IOException)
                {
                    toDebugInstead = true;
                }
                toDebugInitialized = true;
            }

#if DEBUG
            if (toDebugInstead)
            {
                Debug.WriteLine(o);
            }
            else
#endif
                Console.WriteLine(o);
        }

        /// Writes a string or something to System.Console, and fires an optional OtherOutput event for use in the node
        /// <summary>
        ///     Writes a string or something to System.Debug, and fires an optional OtherOutput event for use in the node
        /// </summary>
        /// <param name="o"> A string or something to print </param>
        public static void WriteLine(object o)
        {
            _writeline(o);
        }

        /// Writes a formatted something to System.Console, and fires an optional OtherOutput event for use in the node
        /// <summary>
        ///     Writes a formatted something to System.Debug, and fires an optional OtherOutput event for use in the node
        /// </summary>
        /// <param name="format">Format string</param>
        /// <param name="args">Stuff to format</param>
        public static void WriteLine(string format, params object[] args)
        {
            if (args != null && args.Length > 0)
                _writeline(string.Format(format, args));
            else
                _writeline(format);
        }
    }

    /// <summary>
    ///     Everything happens here.
    /// </summary>
    public static class ROS
    {
        public static TimerManager timer_manager = new TimerManager();

        public static CallbackQueue GlobalCallbackQueue;
        internal static bool initialized, started, atexit_registered, _ok;

        internal static bool _shutting_down, shutdown_requested;
        internal static int init_options;

        /// <summary>
        ///     Means of setting ROS_MASTER_URI programatically before Init is called
        ///     Order of precedence: __master:=... > this variable > User Environment Variable > System Environment Variable
        /// </summary>
        public static string ROS_MASTER_URI;

        /// <summary>
        ///     Means of setting ROS_HOSTNAME directly before Init is called
        ///     Order of precedence: __hostname:=... > this variable > User Environment Variable > System Environment Variable
        /// </summary>
        public static string ROS_HOSTNAME;

        /// <summary>
        ///     Means of setting ROS_IP directly before Init is called
        ///     Order of precedence: __ip:=... > this variable > User Environment Variable > System Environment Variable
        /// </summary>
        public static string ROS_IP;

        private static object start_mutex = new object();

        /// <summary>
        ///     general global sleep time in miliseconds
        /// </summary>
        public static int WallDuration = 10;

        public static NodeHandle GlobalNodeHandle;
        private static object shutting_down_mutex = new object();

        //last sim time time
        private static TimeSpan lastSimTime;
        //last sim time received time (wall)
        private static TimeSpan lastSimTimeReceived;

        private static readonly string ROSOUT_FMAT = "{0} {1}";
        private static readonly string ROSOUT_DEBUG_PREFIX = "[Debug]";
        private static readonly string ROSOUT_INFO_PREFIX  = "[Info ]";
        private static readonly string ROSOUT_WARN_PREFIX  = "[Warn ]";
        private static readonly string ROSOUT_ERROR_PREFIX = "[Error]";
        private static readonly string ROSOUT_FATAL_PREFIX = "[FATAL]";
   
        private static Dictionary<RosOutAppender.ROSOUT_LEVEL, string> ROSOUT_PREFIX =
            new Dictionary<RosOutAppender.ROSOUT_LEVEL, string> {
                { RosOutAppender.ROSOUT_LEVEL.DEBUG, ROSOUT_DEBUG_PREFIX },
                { RosOutAppender.ROSOUT_LEVEL.INFO, ROSOUT_INFO_PREFIX },
                { RosOutAppender.ROSOUT_LEVEL.WARN, ROSOUT_WARN_PREFIX },
                { RosOutAppender.ROSOUT_LEVEL.ERROR, ROSOUT_ERROR_PREFIX },
                { RosOutAppender.ROSOUT_LEVEL.FATAL, ROSOUT_FATAL_PREFIX }
            };

        public static bool shutting_down
        {
            get { return _shutting_down; }
        }

        /// <summary>
        ///     True if ROS is ok, false if not
        /// </summary>
        public static bool ok
        {
            get { return _ok; }
        }

        private static string _processname = null;
        public static string ProcessName { get { if (_processname == null) _processname = Process.GetCurrentProcess().ProcessName; return _processname; } }
        private const string VISUAL_STUDIO_EXE_NAME = "devenv";
        public static bool IsVisualStudio { get { return ProcessName == VISUAL_STUDIO_EXE_NAME; } }
        /// <summary>
        ///     Gets the current thread's TID, emulating the behavior ROS has in a more interprocess situation on xnix
        /// </summary>
        /// <returns> </returns>
        public static UInt64 getPID()
        {
            return (UInt64)
                Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        ///     Turns a DateTime into a Time struct
        /// </summary>
        /// <param name="time"> DateTime to convert </param>
        /// <returns> containing secs, nanosecs since 1/1/1970 </returns>
        public static m.Time GetTime(DateTime time)
        {
            return GetTime<m.Time>(time.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)));
        }

        #region time helpers

        public static long ticksFromData(TimeData data)
        {
            return data.sec * TimeSpan.TicksPerSecond + (uint)Math.Floor(data.nsec / 100.0);

        }

        public static TimeData ticksToData(long ticks)
        {
            return ticksToData((ulong)ticks);
        }

        public static TimeData ticksToData(ulong ticks)
        {
            ulong seconds = (((ulong)Math.Floor(1.0 * ticks / TimeSpan.TicksPerSecond)));
            ulong nanoseconds = 100 * (ticks % TimeSpan.TicksPerSecond);
            return new TimeData((uint)seconds, (uint)nanoseconds);
        }

        #endregion

        /// <summary>
        ///     Turns a std_msgs.Time into a DateTime
        /// </summary>
        /// <param name="time"> std_msgs.Time to convert </param>
        /// <returns> a DateTime </returns>
        public static DateTime GetTime(m.Time time)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0).Add(new TimeSpan(ticksFromData(time.data)));
        }

        /// <summary>
        ///     Turns a std_msgs.Duration into a TimeSpan
        /// </summary>
        /// <param name="time"> std_msgs.Duration to convert </param>
        /// <returns> a TimeSpan </returns>
        public static TimeSpan GetTime(m.Duration duration)
        {
            return new TimeSpan(ticksFromData(duration.data));
        }

        public static T GetTime<T>(TimeSpan ts) where T : IRosMessage, new()
        {
            T test = Activator.CreateInstance(typeof(T), GetTime(ts)) as T;
            return test;
        }

        /// <summary>
        ///     Turns a TimeSpan into a Time (not a Duration, although it sorta is)
        /// </summary>
        /// <param name="timestamp"> The timespan to convert to seconds/nanoseconds </param>
        /// <returns> a time struct </returns>
        public static TimeData GetTime(TimeSpan timestamp)
        {
            if (lastSimTimeReceived != default(TimeSpan))
            {
                timestamp = timestamp.Subtract(lastSimTimeReceived).Add(lastSimTime);
            }
            return ticksToData(timestamp.Ticks);
        }

        /// <summary>
        ///     Gets the current time as secs/nsecs
        /// </summary>
        /// <returns> </returns>
        public static m.Time GetTime()
        {
            return GetTime(DateTime.Now);
        }

        private static void SimTimeCallback(TimeSpan ts)
        {
            lastSimTime = ts;
            lastSimTimeReceived = DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0));
        }

        /// <summary>
        ///     This is self-explanatory
        /// </summary>
        /// <param name="type"> The type of message to make </param>
        /// <returns> A message of that type </returns>
        internal static IRosMessage MakeMessage(MsgTypes type)
        {
            return IRosMessage.generate(type);
        }

        /// <summary>
        ///     If this happens, then the fact that there's a static function called FREAKOUT exists is the least of your problems.
        /// </summary>
        public static void FREAKOUT()
        {
            if (ROS.ProcessName != "devenv")
                throw new Exception("ROS IS FREAKING OUT!");
        }
        
        public class ONLY_AUTO_PARAMS
        {
            private ONLY_AUTO_PARAMS() {}
        }

        public delegate void WriteDelegate(object format, params object[] args);

        /// <summary>
        ///     ROS_INFO(...)
        /// </summary>
        public static WriteDelegate Info(ONLY_AUTO_PARAMS CAPTURE_CALL_SITE = null, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
            return (format, args) => _rosout(format, args, RosOutAppender.ROSOUT_LEVEL.INFO, new CallerInfo { MemberName = memberName, FilePath = filePath, LineNumber = lineNumber });
        }

        /// <summary>
        ///     ROS_DEBUG(...) (formatted)
        /// </summary>
        public static WriteDelegate Debug(ONLY_AUTO_PARAMS CAPTURE_CALL_SITE = null, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
            return (format, args) => _rosout(format, args, RosOutAppender.ROSOUT_LEVEL.DEBUG, new CallerInfo { MemberName = memberName, FilePath = filePath, LineNumber = lineNumber });
        }

        /// <summary>
        ///     ROS_INFO(...) (formatted)
        /// </summary>
        public static WriteDelegate Error(ONLY_AUTO_PARAMS CAPTURE_CALL_SITE = null, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
            return (format, args) => _rosout(format, args, RosOutAppender.ROSOUT_LEVEL.ERROR, new CallerInfo { MemberName = memberName, FilePath = filePath, LineNumber = lineNumber });
        }

        /// <summary>
        ///     ROS_WARN(...) (formatted)
        /// </summary>
        public static WriteDelegate Warn(ONLY_AUTO_PARAMS CAPTURE_CALL_SITE = null, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
            return (format, args) => _rosout(format, args, RosOutAppender.ROSOUT_LEVEL.WARN, new CallerInfo { MemberName = memberName, FilePath = filePath, LineNumber = lineNumber });
        }

        private static void _rosout(object format, object[] args, RosOutAppender.ROSOUT_LEVEL level, CallerInfo callerInfo)
        {
            if (format == null)
                throw new ArgumentNullException(nameof(format));

            string text = (args == null || args.Length == 0) ? format.ToString() : string.Format((string)format, args);
            
            bool printit = true;
            if (level == RosOutAppender.ROSOUT_LEVEL.DEBUG)
            {
#if !DEBUG
                printit = false;
#endif
            }
            if (printit)
                EDB.WriteLine(ROSOUT_FMAT, ROSOUT_PREFIX[level], text);
            RosOutAppender.Instance.Append(text, level, callerInfo);
        }

        /// <summary>
        /// Will make all XmlRpc calls to master that _would_already_wait_for_master_ wait INDEFINITELY
        /// </summary>
        public static void WaitForMaster()
        {
            master.retryTimeout = TimeSpan.FromTicks(0);
        }

        /// <summary>
        ///     Initializes ROS so nodehandles and nodes can exist
        /// </summary>
        /// <param name="args"> argv - parsed for remapping args (AND PARAMS??) </param>
        /// <param name="name"> the node's name </param>
        //TODO make sure params are parsed
        public static void Init(string[] args, string name)
        {
            Init(args, name, 0);
        }

        /// <summary>
        ///     Initializes ROS so nodehandles and nodes can exist
        /// </summary>
        /// <param name="args"> argv - parsed for remapping args (AND PARAMS??) </param>
        /// <param name="name"> the node's name </param>
        /// <param name="options"> options? </param>
        public static void Init(string[] args, string name, int options)
        {
            if (ROS.ProcessName == "devenv")
                return;
            // ROS_MASTER_URI/ROS_HOSTNAME definition precedence:
            // 1. explicitely set by program
            // 2. passed in as remap argument
            // 3. environment variable

            IDictionary remapping;
            if (RemappingHelper.GetRemappings(ref args, out remapping))
                Init(remapping, name, options);
            else
                throw new Exception("Init failed");
        }

        /// <summary>
        ///     Initializes ROS so nodehandles and nodes can exist
        /// </summary>
        /// <param name="remapping_args"> Dictionary of remapping args </param>
        /// <param name="name"> node name </param>
        internal static void Init(IDictionary remapping_args, string name)
        {
            Init(remapping_args, name, 0);
        }

        /// <summary>
        ///     Initializes ROS so nodehandles and nodes can exist
        /// </summary>
        /// <param name="remapping_args"> dictionary of remapping args </param>
        /// <param name="name"> node name </param>
        /// <param name="options"> options? </param>
        internal static void Init(IDictionary remapping_args, string name, int options)
        {
            // if we haven't sunk our fangs into the processes jugular so we can tell
            //    when it has stopped kicking, do so now
            if (!atexit_registered)
            {
                atexit_registered = true;
                Process.GetCurrentProcess().EnableRaisingEvents = true;
                Process.GetCurrentProcess().Exited += (o, args) =>
                {
                    _shutdown();
                    waitForShutdown();
                };
                Console.CancelKeyPress += (o, args) =>
                {
                    _shutdown();
                    waitForShutdown();
                    args.Cancel = true;
                };
            }

            // this needs to exist for connections and stuff to happen
            if (GlobalCallbackQueue == null)
            {
                GlobalCallbackQueue = new CallbackQueue();
            }

            // kick the tires and light the fires
            if (!initialized)
            {
                init_options = options;
                _ok = true;
                network.init(remapping_args);
                master.init(remapping_args);
                this_node.Init(name, remapping_args, options);
                Param.init(remapping_args);
                SimTime.instance.SimTimeEvent += SimTimeCallback;
                initialized = true;
                GlobalNodeHandle = new NodeHandle(this_node.Namespace, remapping_args);
                RosOutAppender.Instance.start();
            }
        }

        /// <summary>
        ///     shutdowns are async with the call to shutdown. This delays shutting down ROS feels like it.
        /// </summary>
        internal static void checkForShutdown()
        {
            lock (shutting_down_mutex)
            {
                if (!shutdown_requested || _shutting_down)
                    return;
            }
            _shutdown();
            shutdown_requested = false;
        }

        /// <summary>
        ///     This is called when rosnode kill is invoked, or something
        /// </summary>
        /// <param name="p"> pointer to unmanaged XmlRpcValue containing params </param>
        /// <param name="r"> pointer to unmanaged XmlRpcValue that will contain return value </param>
        private static void shutdownCallback(XmlRpcValue parms, XmlRpcValue r)
        {
            int num_params = 0;
            if (parms.Type == XmlRpcValue.ValueType.TypeArray)
                num_params = parms.Size;
            if (num_params > 1)
            {
                string reason = parms[1].Get<string>();
                EDB.WriteLine("Shutdown request received.");
                EDB.WriteLine("Reason given for shutdown: [" + reason + "]");
                shutdown();
            }
            XmlRpcManager.Instance.responseInt(1, "", 0)(r);
        }

        /// <summary>
        ///     Hang the current thread until ROS shuts down
        /// </summary>
        public static void waitForShutdown()
        {
            while (_ok)
            {
                Thread.Sleep(WallDuration);
            }
        }

        /// <summary>
        ///     Finishes intialization This is called by the first NodeHandle when it initializes
        /// </summary>
        internal static void start()
        {
            lock (start_mutex)
            {
                if (started) return;

                PollManager.Instance.addPollThreadListener(checkForShutdown);
                XmlRpcManager.Instance.bind("shutdown", shutdownCallback);
                //initInternalTimerManager();
                TopicManager.Instance.Start();
                try
                {
                    ServiceManager.Instance.Start();
                }
                catch (Exception e)
                {
                    EDB.WriteLine(e);
                }
                ConnectionManager.Instance.Start();
                PollManager.Instance.Start();
                XmlRpcManager.Instance.Start();

                //Time.Init();
                GlobalCallbackQueue.Enable();
                shutdown_requested = false;
                _shutting_down = false;
                started = true;
                _ok = true;
            }
        }

        /// <summary>
        ///     self explanatory
        /// </summary>
        /// <returns> guess </returns>
        public static bool isStarted()
        {
            return started;
        }

        /// <summary>
        ///     Tells ROS that it should shutdown the next time it feels like doing so.
        /// </summary>
        public static void shutdown()
        {
            shutdown_requested = true;
        }

        /// <summary>
        ///     Kills all the things. Called by checkForShutdown
        /// </summary>
        private static void _shutdown()
        {
            lock (shutting_down_mutex)
            {
                if (_shutting_down)
                    return;
                _shutting_down = true;

                EDB.WriteLine("ROS is shutting down.");
            }

            if (started)
            {
                started = false;
                _ok = false;
                RosOutAppender.Instance.shutdown();
                GlobalNodeHandle.shutdown();
                GlobalCallbackQueue.Disable();
                GlobalCallbackQueue.Clear();
                TopicManager.Instance.shutdown();
                ServiceManager.Instance.shutdown();
                PollManager.Instance.shutdown();
                XmlRpcManager.Instance.shutdown();
                ConnectionManager.Instance.shutdown();
            }
        }
    }

    /// <summary>
    ///     This is probably useless
    /// </summary>
    public enum InitOption
    {
        NosigintHandler = 1 << 0,
        AnonymousName = 1 << 1,
        NoRousout = 1 << 2
    }
}
