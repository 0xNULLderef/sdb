//
// The MIT License (MIT)
//
// Copyright (c) 2018 Alex Rønne Petersen
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Mono.Debugger.Client
{
    static class LibEdit
    {
        [DllImport("libedit", EntryPoint = "el_init")]
        private static extern IntPtr _Initialize(string prog, IntPtr fin, IntPtr fout, IntPtr ferr);

        [DllImport("libc", EntryPoint = "dlopen")]
        private static extern IntPtr _dlopen(string path, int flags);

        [DllImport("libc", EntryPoint = "dlsym")]
        private static extern IntPtr _dlsym(IntPtr handle, string symbol);

        public static IntPtr Initialize(string prog)
        {
            // RTLD_NOLOAD | RTLD_NOW = 4 | 2 = 6
            var libc = _dlopen("libc.so.6", 6);
            if (libc == IntPtr.Zero)
                throw new FileNotFoundException("Could not load libc");

            var stdinPtr = _dlsym(libc, "stdin");
            var stdoutPtr = _dlsym(libc, "stdout");
            var stderrPtr = _dlsym(libc, "stderr");
            if (stdoutPtr == IntPtr.Zero || stderrPtr == IntPtr.Zero || stdinPtr == IntPtr.Zero)
                throw new EntryPointNotFoundException("Could not find libc std handles");

            unsafe {
                // BIG unsafe but we checked so *should* be fine
                var stdin = *(IntPtr *)stdinPtr;
                var stdout = *(IntPtr *)stdoutPtr;
                var stderr = *(IntPtr *)stderrPtr;    
            
                return _Initialize(prog, stdin, stdout, stderr);
            }
        }

        [DllImport("libedit", EntryPoint = "el_end")]
        public static extern void End(IntPtr e);

        [DllImport("libedit", EntryPoint = "el_gets")]
        private static extern IntPtr _ReadLine(IntPtr e, out int count);

        // we wrap this since the clr wants to free the string REALLY badly
        public static string ReadLine(IntPtr e, out int count)
        {
            return Marshal.PtrToStringAnsi(_ReadLine(e, out count));
        }

        // overloads for el_set
        private const int EL_HIST = 10;
        [DllImport("libedit", EntryPoint = "el_set")]
        private static extern int _SetHist(IntPtr e, int op, IntPtr func, IntPtr hist);

        public static int SetHistDefaultFunc(IntPtr e, IntPtr hist)
        {
            // RTLD_NOLOAD | RTLD_NOW = 4 | 2 = 6
            var libedit = _dlopen("libedit.so.0", 6);
            if (libedit == IntPtr.Zero)
                throw new FileNotFoundException("Could not load libedit");

            var func = _dlsym(libedit, "history");
            if (func == IntPtr.Zero)
                throw new EntryPointNotFoundException("Could not find libedit history function");

            return _SetHist(e, EL_HIST, func, hist);
        }

        private const int EL_PROMPT_ESC = 21;
        public delegate string PromptEscDelegate(IntPtr e);
        [DllImport("libedit", EntryPoint = "el_set")]
        private static extern int _SetPromptEsc(IntPtr e, int op, PromptEscDelegate prompt, char c);

        public static int SetPromptEsc(IntPtr e, PromptEscDelegate prompt, char c)
        {
            // EL_PROMPT_ESC = 21
            return _SetPromptEsc(e, EL_PROMPT_ESC, prompt, c);
        }

        private struct HistEvent {
            public int num;
            public IntPtr str;
        };

        [DllImport("libedit", EntryPoint = "history_init")]
        public static extern IntPtr HistoryInitialize();

        [DllImport("libedit", EntryPoint = "history_end")]
        public static extern void HistoryEnd(IntPtr hist);

        private const int H_SETSIZE = 1;
        [DllImport("libedit", EntryPoint = "history")]
        private static extern int _HistorySetSize(IntPtr hist, out HistEvent ev, int op, int size);

        public static int HistorySetSize(IntPtr hist, int size)
        {
            return _HistorySetSize(hist, out HistEvent _, H_SETSIZE, size);
        }

        private const int H_ENTER = 10;
        [DllImport("libedit", EntryPoint = "history")]
        private static extern int _HistoryEnter(IntPtr hist, out HistEvent ev, int op, string str);

        public static int HistoryEnter(IntPtr hist, string str)
        {
            return _HistoryEnter(hist, out HistEvent _, H_ENTER, str);
        }
    }
}
