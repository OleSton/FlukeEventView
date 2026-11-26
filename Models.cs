// SPDX-License-Identifier: MIT
// Copyright (c) 2026 OleSton
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FlukeEventView
{
    /// <summary>
    /// Перечисление доступных языков интерфейса программы (Русский / Английский).
    /// </summary>
    public enum AppLang
    {
        Ru = 0,
        En = 1
    }

    /// <summary>
    /// Категории событий для фильтрации журнала по типам помех.
    /// </summary>
    public enum EventCategory
    {
        All = 0,          // Все события
        HnSwell = 1,      // Перенапряжение Фаза-Нейтраль (H-N Swell)
        HnSag = 2,        // Провал Фаза-Нейтраль (H-N Sag)
        HnTransient = 3,  // Высоковольтный импульс Фаза-Нейтраль (H-N Transient)
        NgSwell = 4,      // Превышение Нейтраль-Земля (N-G Swell)
        NgTransient = 5,  // Высоковольтный импульс Нейтраль-Земля (N-G Transient)
        HighFreq = 6,     // Высокая частота сети (High Frequency)
        LowFreq = 7,      // Низкая частота сети (Low Frequency)
        Outage = 8,       // Обрыв питания (Outage)
        OpenOnly = 9,     // Только активные незавершенные события (Open Event)
        LossOfSync = 10   // Потеря синхронизации (обнаруживается программой, не прибором)
    }

    /// <summary>
    /// Главная форма приложения Fluke EventView VR101.
    /// </summary>
    public class SiteData
    {
        public bool HasValidData { get; set; } = false;

        public string Description { get; set; } = "";
        public int SerialNumber { get; set; } = 0;
        public int FirmwareVersion { get; set; } = 0;
        public byte OptionsByte { get; set; } = 0x00;

        public uint ElapsedTicks { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public bool Is220V { get; set; } = true;
        public bool Is50Hz { get; set; } = true;
        public bool IsFifo { get; set; } = true;
        public bool FlashLed { get; set; } = true;
        public bool NgScale025 { get; set; } = false;
        public bool IsMemoryFull { get; set; } = false;

        public int HnSag { get; set; } = 210;
        public int HnSwell { get; set; } = 256;
        public int HnImpulse { get; set; } = 200;
        public double NgSurge { get; set; } = 10.0;
        public int NgImpulse { get; set; } = 100;
        public double MinFreq { get; set; } = 48.8;
        public double MaxFreq { get; set; } = 51.2;

        public double LiveHnVolt { get; set; }
        public double LiveNgVolt { get; set; }

        /// <summary>
        /// true — регистратор измеряет канал «нейтраль — земля» (режим IT, бит 0x10 байта 51).
        /// В режиме TN канал не используется, и оригинальный EventView показывает 0 В.
        /// </summary>
        public bool NgChannelEnabled { get; set; }
        public double LiveFreq { get; set; }

        public int StoredEventCount { get; set; }
        public byte[] RawHeader88 { get; set; } = new byte[88];
        public byte[] RawHeader150 { get; set; } = new byte[150];

        public List<EventRecord> OpenEvents { get; set; } = new List<EventRecord>();
        public List<EventRecord> Events { get; set; } = new List<EventRecord>();

        /// <summary>
        /// Приводит описание объекта из памяти прибора в читаемый вид: управляющие символы
        /// (признак повреждённых данных при севшей батарее) заменяются на «?», хвост обрезается.
        /// </summary>
        private static string CleanDescription(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (c == '\0') continue;
                sb.Append((c < 0x20 || c == 0x7F) ? '?' : c);
            }
            return sb.ToString().Trim(' ', '?').Length == 0 ? string.Empty : sb.ToString().TrimEnd(' ');
        }

        public static SiteData ParseFromDeviceHeader(byte[] h, DateTime nowTime)
        {
            var s = new SiteData { HasValidData = true };
            Array.Copy(h, 0, s.RawHeader88, 0, Math.Min(88, h.Length));

            s.ElapsedTicks = (uint)((h[0] << 16) | (h[1] << 8) | h[2]);

            uint endTicks = (uint)((h[82] << 16) | (h[83] << 8) | h[84]);
            if (endTicks == 0 || endTicks > s.ElapsedTicks) endTicks = s.ElapsedTicks;

            s.StartTime = nowTime.AddSeconds(-(s.ElapsedTicks * 8.0));
            s.EndTime = s.StartTime.AddSeconds(endTicks * 8.0);

            byte b51 = h[51];
            s.Is220V = (b51 & 0x80) != 0;
            s.FirmwareVersion = b51 & 0x0F;
            s.NgScale025 = (b51 & 0x10) != 0;
            double vMult = s.Is220V ? 2.0 : 1.0;

            s.LiveHnVolt = h[3] * vMult;

            // Канал «нейтраль — земля» активен только в режиме IT (бит 0x10 байта 51).
            // В режиме TN прибор его не измеряет, и оригинальный EventView показывает 0 В,
            // хотя в байте h[4] при этом лежит «плавающий» шум АЦП (например, 0x70 = 112).
            s.NgChannelEnabled = (b51 & 0x10) != 0;
            s.LiveNgVolt = s.NgChannelEnabled ? (h[4] * (s.NgScale025 ? 0.25 : 1.0)) : 0.0;

            int liveFreqWord = (h[56] << 8) | h[57];
            s.LiveFreq = liveFreqWord > 100 ? Math.Round(57600.0 / liveFreqWord, 1) : 50.0;

            s.OptionsByte = h[6];
            s.Is50Hz = (s.OptionsByte & 0x04) != 0;
            s.FlashLed = (s.OptionsByte & 0x02) == 0;
            s.IsFifo = (s.OptionsByte & 0x01) != 0;

            int minFreqWord = (h[7] << 8) | h[8];
            int maxFreqWord = (h[9] << 8) | h[10];
            if (minFreqWord > 100) s.MinFreq = Math.Round(57600.0 / minFreqWord, 1);
            if (maxFreqWord > 100) s.MaxFreq = Math.Round(57600.0 / maxFreqWord, 1);

            s.HnSag = (int)Math.Round(h[11] * vMult);
            s.HnSwell = (int)Math.Round(h[12] * vMult);
            s.HnImpulse = h[13] * 10;
            s.NgSurge = s.NgScale025 ? Math.Round(h[14] * 0.25, 1) : h[14];
            s.NgImpulse = h[15] * 10;

            s.Description = CleanDescription(Encoding.ASCII.GetString(h, 20, 30));
            s.SerialNumber = (h[52] << 8) | h[53];

            int ptrBytes = (h[54] << 8) | h[55];
            if (h[50] != 0)
            {
                s.IsMemoryFull = true;
                s.StoredEventCount = 4000;
            }
            else
            {
                s.IsMemoryFull = false;
                s.StoredEventCount = ptrBytes / 8;
            }

            if (s.StoredEventCount > 0)
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    byte[] slotBytes = new byte[8];
                    Array.Copy(h, 64 + slot * 8, slotBytes, 0, 8);
                    if (slotBytes[0] != 0x00 && slotBytes[0] != 0xFF)
                    {
                        var openEv = EventRecord.Decode(slotBytes, 0, s.StartTime, s.Is220V, s.Is50Hz, s.NgScale025, true);
                        s.OpenEvents.Add(openEv);
                    }
                }
            }

            if (s.OpenEvents.Count > 0)
            {
                s.StartTime = s.OpenEvents[0].Timestamp.AddSeconds(-16);
            }

            return s;
        }

        public void ParseAllEvents(byte[] rawBuffer)
        {
            Events.Clear();
            int maxEvents = Math.Min(StoredEventCount, rawBuffer.Length / 8);
            for (int i = 0; i < maxEvents; i++)
            {
                byte[] b = new byte[8];
                Array.Copy(rawBuffer, i * 8, b, 0, 8);
                if (b[0] == 0xFF || b[0] == 0x00) continue;
                Events.Add(EventRecord.Decode(b, i + 1, StartTime, Is220V, Is50Hz, NgScale025, false));
            }
        }

        public static SiteData LoadFromVerFile(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 152)
                throw new InvalidDataException("Файл .VER поврежден или имеет неверный размер (< 152 байт).");

            var s = new SiteData { HasValidData = true };
            Array.Copy(data, 2, s.RawHeader150, 0, 150);

            s.EndTime = DateTime.FromOADate(BitConverter.ToDouble(data, 2));
            s.StartTime = DateTime.FromOADate(BitConverter.ToDouble(data, 10));

            s.ElapsedTicks = BitConverter.ToUInt32(data, 18);
            s.SerialNumber = BitConverter.ToInt32(data, 22);

            byte b51 = data[92];
            if (b51 == 0) b51 = 0x89;
            s.Is220V = (b51 & 0x80) != 0;
            s.FirmwareVersion = b51 & 0x0F;
            s.NgScale025 = (b51 & 0x10) != 0;
            double vMult = s.Is220V ? 2.0 : 1.0;

            s.LiveHnVolt = BitConverter.ToUInt16(data, 26) * vMult;

            // То же правило, что и при чтении заголовка прибора: без канала N-G (режим TN) — 0 В
            s.NgChannelEnabled = (b51 & 0x10) != 0;
            s.LiveNgVolt = s.NgChannelEnabled ? (data[29] * (s.NgScale025 ? 0.25 : 1.0)) : 0.0;
            double savedFreq = BitConverter.ToDouble(data, 102);
            s.LiveFreq = (savedFreq > 40.0 && savedFreq < 70.0) ? Math.Round(savedFreq, 1) : 50.0;

            s.OptionsByte = data[32];
            s.Is50Hz = (s.OptionsByte & 0x04) != 0;
            s.FlashLed = (s.OptionsByte & 0x02) == 0;
            s.IsFifo = (s.OptionsByte & 0x01) != 0;

            int wordA = BitConverter.ToInt32(data, 34);
            int wordB = BitConverter.ToInt32(data, 38);
            if (wordA > 100 && wordB > 100)
            {
                double fA = Math.Round(57600.0 / wordA, 1);
                double fB = Math.Round(57600.0 / wordB, 1);
                s.MinFreq = Math.Min(fA, fB);
                s.MaxFreq = Math.Max(fA, fB);
            }

            s.HnSag = (int)Math.Round(BitConverter.ToUInt16(data, 42) * vMult);
            s.HnSwell = (int)Math.Round(BitConverter.ToUInt16(data, 44) * vMult);
            s.HnImpulse = BitConverter.ToUInt16(data, 46) * 10;
            ushort rawNgSwell = BitConverter.ToUInt16(data, 48);
            s.NgSurge = s.NgScale025 ? Math.Round(rawNgSwell * 0.25, 1) : rawNgSwell;
            s.NgImpulse = BitConverter.ToUInt16(data, 50) * 10;

            s.Description = CleanDescription(Encoding.ASCII.GetString(data, 52, 30));

            int ptrBytes = BitConverter.ToInt32(data, 110);
            int totBytes = BitConverter.ToInt32(data, 114);
            int headerEventCount = Math.Max(ptrBytes, totBytes) / 8;
            int remainingBytes = data.Length - 152;

            for (int slot = 0; slot < 3; slot++)
            {
                byte[] slotBytes = new byte[8];
                Array.Copy(data, 126 + slot * 8, slotBytes, 0, 8);
                if (slotBytes[0] != 0x00 && slotBytes[0] != 0xFF)
                {
                    var openEv = EventRecord.Decode(slotBytes, 0, s.StartTime, s.Is220V, s.Is50Hz, s.NgScale025, true);
                    s.OpenEvents.Add(openEv);
                }
            }

            if (remainingBytes >= 16 && remainingBytes % 16 == 0)
            {
                // Число событий определяем по фактической длине файла. Поля заголовка
                // (смещения 110 и 114) в старых файлах бывают недостоверны: например,
                // SAMPLE.VER содержит 194 записи, а поле заголовка даёт 544 / 8 = 68 событий.
                int evCount = remainingBytes / 16;
                if (headerEventCount > 0 && headerEventCount != evCount)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"VER: файл содержит {evCount} событий, заголовок заявляет {headerEventCount}.");
                }

                s.StoredEventCount = evCount;
                byte[] packedEvents = new byte[evCount * 8];
                for (int e = 0; e < evCount; e++)
                {
                    for (int bIdx = 0; bIdx < 8; bIdx++)
                    {
                        packedEvents[e * 8 + bIdx] = data[152 + e * 16 + bIdx * 2];
                    }
                }
                s.ParseAllEvents(packedEvents);
            }
            else if (remainingBytes >= 8)
            {
                s.StoredEventCount = remainingBytes / 8;
                byte[] evBuf = new byte[remainingBytes];
                Array.Copy(data, 152, evBuf, 0, remainingBytes);
                s.ParseAllEvents(evBuf);
            }

            return s;
        }

        public void SaveToVerFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Encoding.ASCII.GetBytes(".2"));
                byte[] hdr = (byte[])RawHeader150.Clone();

                double vDiv = Is220V ? 2.0 : 1.0;
                BitConverter.GetBytes(EndTime.ToOADate()).CopyTo(hdr, 0);
                BitConverter.GetBytes(StartTime.ToOADate()).CopyTo(hdr, 8);
                BitConverter.GetBytes((int)ElapsedTicks).CopyTo(hdr, 16);
                BitConverter.GetBytes(SerialNumber).CopyTo(hdr, 20);
                BitConverter.GetBytes((short)Math.Round(LiveHnVolt / vDiv)).CopyTo(hdr, 24);
                BitConverter.GetBytes((short)OptionsByte).CopyTo(hdr, 30);

                int maxFWord = (int)Math.Round(57600.0 / MaxFreq);
                int minFWord = (int)Math.Round(57600.0 / MinFreq);
                BitConverter.GetBytes(maxFWord).CopyTo(hdr, 32);
                BitConverter.GetBytes(minFWord).CopyTo(hdr, 36);

                BitConverter.GetBytes((short)Math.Round(HnSag / vDiv)).CopyTo(hdr, 40);
                BitConverter.GetBytes((short)Math.Round(HnSwell / vDiv)).CopyTo(hdr, 42);
                BitConverter.GetBytes((short)(HnImpulse / 10)).CopyTo(hdr, 44);
                BitConverter.GetBytes((short)(NgScale025 ? Math.Round(NgSurge * 4.0) : Math.Round(NgSurge))).CopyTo(hdr, 46);
                BitConverter.GetBytes((short)(NgImpulse / 10)).CopyTo(hdr, 48);

                string d30 = (Description ?? "").PadRight(30, ' ').Substring(0, 30);
                Encoding.ASCII.GetBytes(d30).CopyTo(hdr, 50);

                BitConverter.GetBytes((short)3).CopyTo(hdr, 80);
                BitConverter.GetBytes(32000).CopyTo(hdr, 82);
                byte b51 = (byte)((Is220V ? 0x80 : 0x00) | (NgScale025 ? 0x10 : 0x00) | (FirmwareVersion & 0x0F));
                BitConverter.GetBytes((short)b51).CopyTo(hdr, 90);
                BitConverter.GetBytes(LiveFreq).CopyTo(hdr, 100);

                BitConverter.GetBytes(Events.Count * 8).CopyTo(hdr, 108);
                BitConverter.GetBytes(Events.Count * 8).CopyTo(hdr, 112);

                for (int slot = 0; slot < Math.Min(3, OpenEvents.Count); slot++)
                {
                    Array.Copy(OpenEvents[slot].Raw, 0, hdr, 124 + slot * 8, 8);
                }

                BitConverter.GetBytes((ushort)(Events.Count > 0 ? 1 : 0)).CopyTo(hdr, 148);
                bw.Write(hdr);

                foreach (var ev in Events)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        bw.Write((ushort)ev.Raw[i]);
                    }
                }
            }
        }
    }

    // =========================================================================
    // КЛАСС ДЕКОДИРОВАНИЯ 8-БАЙТНОЙ ЗАПИСИ СОБЫТИЯ (EventRecord)
    // =========================================================================
    public class EventRecord
    {
        public int Index { get; set; }
        public byte[] Raw { get; set; } = new byte[8];
        public DateTime Timestamp { get; set; }
        public EventCategory Category { get; set; } = EventCategory.All;
        public string EventNameEn { get; set; } = "";
        public string EventNameRu { get; set; } = "";
        public string ExtremeText { get; set; } = "";
        public string EndTextEn { get; set; } = "";
        public string EndTextRu { get; set; } = "";

        public bool IsOpenEvent { get; set; }

        /// <summary>
        /// true — запись создана программой (например, «Loss of Synchronization»),
        /// а не считана из памяти прибора. Такие записи не сохраняются в .VER.
        /// </summary>
        public bool IsSynthetic { get; set; }
        public bool IsTransient { get; set; }
        public bool IsNgTransient { get; set; }
        public int TransientCount { get; set; }
        public bool IsSingleTransient => IsTransient && !IsOpenEvent && TransientCount == 1;
        public int TransientPeakSigned { get; set; }
        public int PhaseDegrees { get; set; }

        public string GetLocalizedEventName(AppLang lang) => lang == AppLang.Ru ? EventNameRu : EventNameEn;
        public string GetLocalizedEndText(AppLang lang) => lang == AppLang.Ru ? EndTextRu : EndTextEn;

        public static EventRecord Decode(byte[] b, int index, DateTime baseStart, bool is220, bool is50, bool ngScale025, bool isOpenEvent)
        {
            var r = new EventRecord { Index = index, Raw = b, IsOpenEvent = isOpenEvent };
            byte type = b[0];

            uint eventTicks = (uint)((b[5] << 16) | (b[6] << 8) | b[7]);
            r.Timestamp = baseStart.AddSeconds(eventTicks * 8.0);

            double vMult = is220 ? 2.0 : 1.0;
            double baseFreq = is50 ? 50.0 : 60.0;
            double ticksPerDeg = is50 ? 3.2 : (960.0 / 360.0);

            uint rawDur = (uint)((b[2] << 16) | (b[3] << 8) | b[4]);
            bool durIn8SecTicks = (type == 0x06 || type == 0x86 || type == 0x08 || type == 0x88);

            string durEn = isOpenEvent ? "Open Event" : (durIn8SecTicks ? FormatTickDuration(rawDur, AppLang.En) : FormatHalfCycles(rawDur, baseFreq, AppLang.En));
            string durRu = isOpenEvent ? "Активное событие" : (durIn8SecTicks ? FormatTickDuration(rawDur, AppLang.Ru) : FormatHalfCycles(rawDur, baseFreq, AppLang.Ru));

            switch (type)
            {
                case 0x01:
                case 0x02:
                    string sign = (type == 0x01) ? "-" : "+";
                    int count = b[2] > 0 ? b[2] : 1;
                    int phaseTicks = (b[3] << 8) | b[4];
                    int deg = (int)Math.Floor((phaseTicks / ticksPerDeg) + 0.625);

                    r.Category = EventCategory.HnTransient;
                    r.IsTransient = true;
                    r.IsNgTransient = false;
                    r.TransientCount = count;
                    r.TransientPeakSigned = (type == 0x01 ? -1 : 1) * (b[1] * 10);
                    r.PhaseDegrees = deg;

                    r.EventNameEn = count == 1 ? "1 H-N Transient" : $"{count} H-N Transients";
                    r.EventNameRu = count == 1 ? "1 Импульс H-N" : $"{count} Импульсов H-N";
                    r.ExtremeText = $"{sign}{b[1] * 10} Vp";
                    r.EndTextEn = $"{deg}°";
                    r.EndTextRu = $"{deg}°";
                    break;

                case 0x03:
                case 0x04:
                    string ngSign = (type == 0x03) ? "-" : "+";
                    int ngCount = b[2] > 0 ? b[2] : 1;
                    int ngTicks = (b[3] << 8) | b[4];
                    int ngDeg = (int)Math.Floor((ngTicks / ticksPerDeg) + 0.625);

                    r.Category = EventCategory.NgTransient;
                    r.IsTransient = true;
                    r.IsNgTransient = true;
                    r.TransientCount = ngCount;
                    r.TransientPeakSigned = (type == 0x03 ? -1 : 1) * (b[1] * 10);
                    r.PhaseDegrees = ngDeg;

                    r.EventNameEn = ngCount == 1 ? "1 N-G Transient" : $"{ngCount} N-G Transients";
                    r.EventNameRu = ngCount == 1 ? "1 Импульс N-G" : $"{ngCount} Импульсов N-G";
                    r.ExtremeText = $"{ngSign}{b[1] * 10} Vp";
                    r.EndTextEn = $"{ngDeg}°";
                    r.EndTextRu = $"{ngDeg}°";
                    break;

                case 0x85:
                case 0x86:
                case 0x16:
                    r.Category = EventCategory.HnSwell;
                    r.EventNameEn = "H-N Swell";
                    r.EventNameRu = "Перенапряжение H-N";
                    r.ExtremeText = $"{(b[1] * vMult):0}   Vrms";
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;

                case 0x05:
                case 0x06:
                case 0x17:
                    if (b[1] == 0)
                    {
                        r.Category = EventCategory.Outage;
                        r.EventNameEn = "Outage";
                        r.EventNameRu = "Обрыв питания";
                        r.ExtremeText = "0 Vrms";
                    }
                    else
                    {
                        r.Category = EventCategory.HnSag;
                        r.EventNameEn = "H-N Sag";
                        r.EventNameRu = "Провал H-N";
                        r.ExtremeText = $"{(b[1] * vMult):0}   Vrms";
                    }
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;

                case 0x07:
                case 0x08:
                    r.Category = EventCategory.LowFreq;
                    r.EventNameEn = "Low Frequency";
                    r.EventNameRu = "Низкая частота";
                    double lowF = baseFreq - ((b[1] - 1) * 0.1);
                    r.ExtremeText = $"{lowF:00.0} Hz";
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;

                case 0x87:
                case 0x88:
                    r.Category = EventCategory.HighFreq;
                    r.EventNameEn = "High Frequency";
                    r.EventNameRu = "Высокая частота";
                    double highF = baseFreq + (b[1] * 0.1);
                    r.ExtremeText = $"{highF:00.0} Hz";
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;

                case 0x12:
                case 0x14:
                case 0x09:
                case 0x89:
                    r.Category = EventCategory.NgSwell;
                    r.EventNameEn = "N-G Swell";
                    r.EventNameRu = "Превышение N-G";
                    double ngV = ngScale025 ? (b[1] * 0.25) : b[1];
                    r.ExtremeText = $"{ngV:0.0} Vrms";
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;

                case 0x80:
                    r.Category = EventCategory.Outage;
                    r.EventNameEn = "Outage";
                    r.EventNameRu = "Обрыв питания";
                    r.ExtremeText = "0 Vrms";
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;

                default:
                    r.Category = EventCategory.All;
                    r.EventNameEn = $"Event 0x{type:X2}";
                    r.EventNameRu = $"Событие 0x{type:X2}";
                    r.ExtremeText = $"{(b[1] * vMult):0} V";
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;
            }

            return r;
        }

        private static string FormatHalfCycles(uint rawHalfCycles, double lineFreq, AppLang lang)
        {
            double cycles = (rawHalfCycles + 1) / 2.0;
            if (cycles < 50.0)
                return lang == AppLang.Ru ? $"{cycles:0.0} период." : $"{cycles:0.0} cycles";

            double seconds = cycles / lineFreq;
            if (seconds < 60.0)
                return lang == AppLang.Ru ? $"{seconds:0.0} сек" : $"{seconds:0.0} seconds";

            int mins = (int)(seconds / 60);
            double remSec = seconds - (mins * 60);
            if (mins < 60)
                return lang == AppLang.Ru ? $"{mins} мин, {remSec:0.0} сек" : $"{mins} min, {remSec:0.0} sec";

            return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        }

        private static string FormatTickDuration(uint ticks, AppLang lang)
        {
            double seconds = ticks * 8.0;
            if (seconds < 60.0)
                return lang == AppLang.Ru ? $"{seconds:0.0} сек" : $"{seconds:0.0} seconds";

            int mins = (int)(seconds / 60);
            int remSec = (int)(seconds - (mins * 60));
            if (mins < 60)
                return lang == AppLang.Ru ? $"{mins} мин, {remSec} сек" : $"{mins} minutes, {remSec} seconds";

            return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        }
    }

    /// <summary>
    /// Окно сервисной калибровки VR101 — аналог «VR101 Calibration» оригинального EventView.
    /// Показывает текущее измерение прибора, принимает показание внешнего вольтметра и
    /// вычисляет поправочный коэффициент. Запись коэффициента в прибор в этой версии
    /// не выполняется: формат хранения калибровки не подтверждён, ошибочная запись
    /// изменила бы показания прибора.
    /// </summary>
    internal sealed class CalibrationForm : Form
    {
        private readonly Func<double> _getLiveVolt;
        private readonly Func<bool> _isConnected;
        private readonly AppLang _lang;
        private readonly Action<string> _log;

        private Label _lblLive;
        private Label _lblMeasuredCaption;
        private NumericUpDown _numMeasured;
        private Label _lblResult;
        private Button _btnRefresh;
        private Button _btnApply;
        private Button _btnCancel;
        private Label _lblNote;

        private string T(string ru, string en) => _lang == AppLang.Ru ? ru : en;

        public CalibrationForm(Func<double> getLiveVolt, Func<bool> isConnected, AppLang lang, Action<string> log)
        {
            _getLiveVolt = getLiveVolt;
            _isConnected = isConnected;
            _lang = lang;
            _log = log;

            this.Text = T("Калибровка VR101", "VR101 Calibration");
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(480, 320);
            this.Font = new Font("MS Sans Serif", 8.5f);

            var fontBig = new Font("Consolas", 12f, FontStyle.Bold);

            var lblLiveCaption = new Label
            {
                Text = T("Показание прибора:", "Recorder reading:"),
                Location = new Point(14, 18),
                AutoSize = true
            };

            _lblLive = new Label
            {
                Text = "---",
                Location = new Point(180, 13),
                AutoSize = true,
                Font = fontBig,
                ForeColor = Color.DarkGreen
            };

            _lblMeasuredCaption = new Label
            {
                Text = T("Измеренное напряжение (Vrms):", "Measured Voltage (Vrms):"),
                Location = new Point(14, 62),
                AutoSize = true
            };

            _numMeasured = new NumericUpDown
            {
                Location = new Point(250, 59),
                Width = 92,
                Minimum = 1,
                Maximum = 500,
                DecimalPlaces = 1,
                Increment = 0.1m
            };

            _btnRefresh = new Button
            {
                Text = T("Обновить", "Refresh"),
                Location = new Point(352, 57),
                Width = 108,
                Height = 27
            };
            _btnRefresh.Click += (s, e) => RefreshReading();

            _lblResult = new Label
            {
                Location = new Point(14, 98),
                AutoSize = false,
                Size = new Size(446, 56),
                ForeColor = Color.DarkSlateBlue,
                Font = new Font("MS Sans Serif", 9f)
            };

            _lblNote = new Label
            {
                Location = new Point(14, 162),
                Size = new Size(446, 84),
                ForeColor = Color.Firebrick,
                Text = T(
                    "Запись калибровочного коэффициента в прибор в этой версии не выполняется: формат хранения калибровки VR101 не подтверждён, а ошибочная запись изменит показания прибора. Окно показывает текущее измерение и рассчитывает поправочный коэффициент для сервисной проверки.",
                    "Writing the calibration value into the recorder is not implemented in this build: the VR101 calibration storage format is not verified, and a wrong write would change the recorder readings. This window shows the live reading and computes the correction factor for servicing.")
            };

            _btnApply = new Button
            {
                Text = T("Рассчитать", "Calculate"),
                Location = new Point(234, 268),
                Width = 110,
                Height = 32
            };
            _btnApply.Click += (s, e) => ApplyCalibration();

            _btnCancel = new Button
            {
                Text = T("Отмена", "Cancel"),
                Location = new Point(352, 268),
                Width = 108,
                Height = 32,
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[]
            {
                lblLiveCaption, _lblLive, _lblMeasuredCaption, _numMeasured, _btnRefresh,
                _lblResult, _lblNote, _btnApply, _btnCancel
            });

            this.AcceptButton = _btnApply;
            this.CancelButton = _btnCancel;
            this.Shown += (s, e) => RefreshReading();
        }

        private void RefreshReading()
        {
            bool connected = _isConnected != null && _isConnected();
            double live = (connected && _getLiveVolt != null) ? _getLiveVolt() : 0;

            if (!connected || live <= 0)
            {
                _lblLive.Text = "---";
                _lblLive.ForeColor = Color.Gray;
                _lblResult.ForeColor = Color.Firebrick;
                _lblResult.Text = T("Связь с прибором потеряна. Подключитесь и включите опрос.",
                                    "Connection with logger has been lost !");
                return;
            }

            _lblLive.Text = live.ToString("0.0") + " " + T("В", "V");
            _lblLive.ForeColor = Color.DarkGreen;

            if (_numMeasured.Value <= 0)
                _numMeasured.Value = (decimal)Math.Round(live, 1);

            double measured = (double)_numMeasured.Value;
            double dev = (measured - live) / live * 100.0;
            double factor = measured / live;

            string verdict;
            Color color;
            if (Math.Abs(dev) < 0.5)
            {
                verdict = T("Отклонение в норме — калибровка не требуется.",
                            "Deviation is within tolerance — calibration is not required.");
                color = Color.DarkGreen;
            }
            else if (Math.Abs(dev) < 5.0)
            {
                verdict = T("Небольшое отклонение — допустимо для общих измерений.",
                            "Slight deviation — acceptable for general measurements.");
                color = Color.DarkSlateBlue;
            }
            else
            {
                verdict = T("Существенное отклонение — требуется сервисная калибровка.",
                            "Significant deviation — service calibration is required.");
                color = Color.Firebrick;
            }

            _lblResult.ForeColor = color;
            _lblResult.Text =
                T($"Отклонение: {dev:+0.00;-0.00;0.00} %   ·   поправочный коэффициент: {factor:0.000}",
                  $"Deviation: {dev:+0.00;-0.00;0.00} %   ·   correction factor: {factor:0.000}")
                + Environment.NewLine + verdict;
        }

        private void ApplyCalibration()
        {
            double live = _getLiveVolt != null ? _getLiveVolt() : 0;
            double measured = (double)_numMeasured.Value;

            if (live <= 0)
            {
                MessageBox.Show(T("Нет текущего измерения прибора.", "No live reading from the recorder."),
                    this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _log?.Invoke($"Calibration calculated: live={live:0.0} V, measured={measured:0.0} V, factor={measured / live:0.000} (write skipped)");

            MessageBox.Show(
                T("Коэффициент рассчитан и записан в журнал программы.\nВ прибор ничего не записывалось — значение сохранено только для протокола.",
                  "The factor was calculated and written to the program log.\nNothing was written to the recorder — the value is recorded for the protocol only."),
                this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
