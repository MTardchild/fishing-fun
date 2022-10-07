using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace FishingFun
{
    public class FishingBot
    {
        public static ILog logger = LogManager.GetLogger("Fishbot");

        private ConsoleKey castKey;
        private ConsoleKey lureKey;
        private ConsoleKey destroyKey;
        private IBobberFinder bobberFinder;
        private IBiteWatcher biteWatcher;
        private bool isEnabled;
        private Stopwatch stopwatch = new Stopwatch();
        private static Random random = new Random();
        private int maxWaitTime = 200;
        private DateTime TimeLastLure = DateTime.Now;

        public event EventHandler<FishingEvent> FishingEventHandler;

        public FishingBot(
            IBobberFinder bobberFinder, 
            IBiteWatcher biteWatcher, 
            ConsoleKey castKey, 
            ConsoleKey lureKey = ConsoleKey.Escape, 
            ConsoleKey destroyKey = ConsoleKey.Escape)
        {
            this.bobberFinder = bobberFinder;
            this.biteWatcher = biteWatcher;
            this.castKey = castKey;
            this.lureKey = lureKey;
            this.destroyKey = destroyKey;

            logger.Info("FishBot Created.");

            FishingEventHandler += (s, e) => { };
        }

        public void Start()
        {
            biteWatcher.FishingEventHandler = (e) => FishingEventHandler?.Invoke(this, e);
            isEnabled = true;
            DoTenMinuteKey();

            while (isEnabled)
            {
                try
                {
                    logger.Info($"Pressing key {castKey} to Cast.");
                    PressTenMinKeyIfDue();
                    FishingEventHandler?.Invoke(this, new FishingEvent { Action = FishingAction.Cast });
                    WowProcess.PressKey(destroyKey);
                    WowProcess.PressKey(castKey);
                    Watch(2000);
                    WaitForBite();
                }
                catch (Exception e)
                {
                    logger.Error(e.ToString());
                    Sleep(2000);
                }
            }

            logger.Error("Bot has Stopped.");
        }

        public void SetCastKey(ConsoleKey castKey)
        {
            this.castKey = castKey;
        }

        private void Watch(int milliseconds)
        {
            bobberFinder.Reset();
            stopwatch.Reset();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < milliseconds)
            {
                bobberFinder.Find();
            }
            stopwatch.Stop();
        }

        public void Stop()
        {
            isEnabled = false;
            logger.Error("Bot is Stopping...");
        }

        private void WaitForBite()
        {
            bobberFinder.Reset();

            var bobberPosition = FindBobber();
            if (bobberPosition == Point.Empty)
            {
                return;
            }

            this.biteWatcher.Reset(bobberPosition);
            logger.Info("Bobber start position: " + bobberPosition);
            var timedTask = new TimedAction((a) => { logger.Info("Fishing timed out!"); }, 25 * 1000, 25);

            // Wait for the bobber to move
            while (isEnabled)
            {
                var currentBobberPosition = FindBobber();
                if (currentBobberPosition == Point.Empty || currentBobberPosition.X == 0) { return; }

                if (biteWatcher.IsBite(currentBobberPosition))
                {
                    Loot(bobberPosition);
                    PressTenMinKeyIfDue();
                    return;
                }

                if (!timedTask.ExecuteIfDue()) { return; }
            }
        }

        private void PressTenMinKeyIfDue()
        {
            if ((DateTime.Now - TimeLastLure).TotalMinutes > 10.1)
                DoTenMinuteKey();
        }

        private void DoTenMinuteKey()
        {
            TimeLastLure = DateTime.Now;
            FishingEventHandler?.Invoke(this, new FishingEvent { Action = FishingAction.Cast });
            WowProcess.PressKey(lureKey);
            Sleep(2750);
        }

        private void Loot(Point bobberPosition)
        {
            logger.Info($"Right clicking mouse to Loot.");
            WowProcess.RightClickMouse(logger, bobberPosition);
            Sleep(1000);
        }

        public void Sleep(int ms)
        {
            ms += random.Next(0, maxWaitTime);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (sw.Elapsed.TotalMilliseconds < ms)
            {
                FlushBuffers();
                Thread.Sleep(25);
            }
        }

        public static void FlushBuffers()
        {
            ILog log = LogManager.GetLogger("Fishbot");
            var logger = log.Logger as Logger;
            if (logger != null)
            {
                foreach (IAppender appender in logger.Appenders)
                {
                    var buffered = appender as BufferingAppenderSkeleton;
                    if (buffered != null)
                    {
                        buffered.Flush();
                    }
                }
            }
        }

        private Point FindBobber()
        {
            var timer = new TimedAction((a) => { logger.Info("Waited seconds for target: " + a.ElapsedSecs); }, 1000, 5);

            while (true)
            {
                var target = this.bobberFinder.Find();
                if (target != Point.Empty || !timer.ExecuteIfDue()) { return target; }
            }
        }
    }
}