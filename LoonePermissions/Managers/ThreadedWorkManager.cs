using System;
using System.Collections.Generic;
using System.Threading;

namespace ChubbyQuokka.LoonePermissions.Managers
{
    public static class ThreadedWorkManager
    {
        static Queue<Action> ThreadedQueue = new Queue<Action>();
        static Queue<Action> ThreadedBuffer = new Queue<Action>();
        static object ThreadedLock = new object();

        static Queue<Action> UnthreadedQueue = new Queue<Action>();
        static Queue<Action> UnthreadedBuffer = new Queue<Action>();
        static object UnthreadedLock = new object();

        static Thread WorkerThread;

        static volatile bool RunThread;

        public static bool IsWorkerThread => Thread.CurrentThread == WorkerThread;

        internal static void Initialize()
        {
            RunThread = true;

            WorkerThread = new Thread(ThreadedWork)
            {
                Name = "LoonePermissions_WorkerThread"
            };

            WorkerThread.Start();
        }

        internal static void Destroy()
        {
            RunThread = false;

            if (WorkerThread != null)
            {
                if (WorkerThread.IsAlive)
                {
                    WorkerThread.Join();
                }

                WorkerThread = null;
            }

            ThreadedBuffer.Clear();
            ThreadedQueue.Clear();

            UnthreadedBuffer.Clear();
            UnthreadedQueue.Clear();
        }

        internal static void Update()
        {
            lock (UnthreadedLock)
            {
                while (UnthreadedBuffer.Count != 0)
                {
                    UnthreadedQueue.Enqueue(UnthreadedBuffer.Dequeue());
                }
            }

            while (UnthreadedQueue.Count != 0)
            {
                UnthreadedQueue.Dequeue().Invoke();
            }
        }

        static void ThreadedWork()
        {
            while (RunThread)
            {
                Thread.Sleep(LoonePermissionsConfig.AdvancedSettings.WorkerThreadSleepTime);

                lock (ThreadedLock)
                {
                    while (ThreadedBuffer.Count != 0)
                    {
                        ThreadedQueue.Enqueue(ThreadedBuffer.Dequeue());
                    }
                }

                while (ThreadedQueue.Count != 0)
                {
                    ThreadedQueue.Dequeue().Invoke();
                }
            }
        }

        public static void EnqueueWorkerThread(Action action)
        {
            lock (ThreadedLock)
            {
                ThreadedBuffer.Enqueue(action);
            }
        }

        public static void EnqueueMainThread(Action action)
        {
            lock (UnthreadedLock)
            {
                UnthreadedBuffer.Enqueue(action);
            }
        }
    }
}
