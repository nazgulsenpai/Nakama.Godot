/**
 * Copyright 2017 The Nakama Authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using Godot;
//using UnityEngine;

namespace Nakama
{
    // TODO(novabyte) Make improvements to method signatures.
    public class NUnityLogger : INLogger
    {
        public void Trace(object message)
        {
            GD.Print(message);
        }

        public void Trace(object message, Exception exception)
        {
            GD.Print(message);
        }

        public void TraceIf(bool condition, object message)
        {
            if (condition) Trace(message);
        }

        public void TraceIf(bool condition, object message, Exception exception)
        {
            if (condition) Trace(message);
        }

        public void TraceFormat(string format, params object[] args)
        {
            GD.Print(format, args);
        }

        public void TraceFormatIf(bool condition, string format, params object[] args)
        {
            if (condition) TraceFormat(format, args);
        }

        public void Debug(object message)
        {
            GD.Print(message);
        }

        public void Debug(object message, Exception exception)
        {
            GD.Print(message);
        }

        public void DebugFormat(string format, params object[] args)
        {
            GD.Print(format, args);
        }

        public void Info(object message)
        {
            GD.Print(message);
        }

        public void Info(object message, Exception exception)
        {
            GD.Print(message);
        }

        public void InfoFormat(string format, params object[] args)
        {
            GD.Print(format, args);
        }

        public void Warn(object message)
        {
            GD.Print("WARNING - " + message);
        }

        public void Warn(object message, Exception exception)
        {
            GD.Print("WARNING - " + message);
        }

        public void WarnFormat(string format, params object[] args)
        {
           GD.Print(format, args);
        }

        public void Error(object message)
        {
            GD.Print(message);
        }

        public void Error(object message, Exception exception)
        {
            GD.Print(message);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            GD.Print(format, args);
        }

        public void Fatal(object message)
        {
            GD.Print(message);
        }

        public void Fatal(object message, Exception exception)
        {
            GD.Print(message);
        }

        public void FatalFormat(string format, params object[] args)
        {
            GD.Print(format, args);
        }
    }
}
