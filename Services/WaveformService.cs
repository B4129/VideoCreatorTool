using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace VideoCreatorWPF.Services
{
    /// <summary>
    /// 音声ファイルから波形データを生成するサービス
    /// </summary>
    public static class WaveformService
    {
        public class WaveformData
        {
            public List<float> Samples { get; set; } = new();
            public float Peak { get; set; }
            public float RMS { get; set; }
            public int SampleCount => Samples.Count;
        }

        /// <summary>
        /// 音声ファイルから波形データを生成（WAV/MP3/OGG/M4A対応）
        /// FFMpegCoreを使用してあらゆるオーディオ形式を解析
        /// </summary>
        public static async Task<WaveformData?> GenerateWaveform(string audioPath, int sampleCount = 100)
        {
            try
            {
                if (!File.Exists(audioPath))
                {
                    Debug.WriteLine($"Waveform: File not found: {audioPath}");
                    return null;
                }

                var extension = Path.GetExtension(audioPath).ToLower();

                // WAVファイルの場合は直接解析
                if (extension == ".wav" && File.Exists(audioPath))
                {
                    var wavData = ParseWavFile(audioPath, sampleCount);
                    if (wavData != null) return wavData;
                }

                // MP3/OGG/M4Aなどの場合はFFMpegCoreを使用して解析
                return await GenerateWaveformViaFfmpeg(audioPath, sampleCount);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Waveform generation error: {ex.Message}");
                return GeneratePlaceholderWaveform(sampleCount);
            }
        }

        /// <summary>
        /// FFMpegCoreを使用して任意のオーディオ形式から波形データを抽出
        /// </summary>
        private static async Task<WaveformData?> GenerateWaveformViaFfmpeg(string audioPath, int sampleCount)
        {
            try
            {
                Debug.WriteLine($"[Waveform] Extracting waveform from: {audioPath}");

                // 一時ファイル出力を使用
                var tempFile = Path.GetTempFileName() + ".raw";

                try
                {
                    Debug.WriteLine($"[Waveform] Using FFMpegCore to extract PCM data");

                    // FFMpegCoreでRaw PCMを抽出（8kHz, mono, float32）
                    var success = await FFMpegCore.FFMpegArguments
                        .FromFileInput(audioPath)
                        .OutputToFile(tempFile, overwrite: true, options => options
                            .ForceFormat("f32le")
                            .WithCustomArgument($"-acodec pcm_f32le -ac 1 -ar 8000"))
                        .ProcessAsynchronously();

                    if (!success || !File.Exists(tempFile))
                    {
                        Debug.WriteLine("[Waveform] FFMpegCore extraction failed");
                        return GeneratePlaceholderWaveform(sampleCount);
                    }

                    // Raw PCMデータ読み込み
                    var rawData = await File.ReadAllBytesAsync(tempFile);

                    if (rawData.Length == 0)
                    {
                        Debug.WriteLine("[Waveform] No PCM data extracted");
                        return GeneratePlaceholderWaveform(sampleCount);
                    }

                    // Float32サンプルに変換
                    var floatSamples = new List<float>();
                    for (int i = 0; i < rawData.Length; i += 4)
                    {
                        if (i + 3 < rawData.Length)
                        {
                            var sample = BitConverter.ToSingle(rawData, i);
                            floatSamples.Add(Math.Abs(sample)); // 絶対値で包絡線
                        }
                    }

                    Debug.WriteLine($"[Waveform] Extracted {floatSamples.Count} samples");

                    if (floatSamples.Count == 0)
                    {
                        return GeneratePlaceholderWaveform(sampleCount);
                    }

                    // 指定サンプル数にダウンサンプリング
                    return DownsampleToTarget(floatSamples, sampleCount);
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        try { File.Delete(tempFile); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FFMpegCore waveform extraction error: {ex.Message}");
                return GeneratePlaceholderWaveform(sampleCount);
            }
        }

        /// <summary>
        /// サンプルデータを目標数にダウンサンプリング（平均値）
        /// </summary>
        private static WaveformData DownsampleToTarget(List<float> samples, int targetCount)
        {
            var data = new WaveformData();
            var step = Math.Max(1, samples.Count / targetCount);

            for (int i = 0; i < samples.Count; i += step)
            {
                var end = Math.Min(i + step, samples.Count);
                var sum = 0.0f;
                var count = 0;
                for (int j = i; j < end; j++)
                {
                    sum += samples[j];
                    count++;
                }
                data.Samples.Add(sum / count);
            }

            // 統計計算
            data.Peak = 0;
            float sumSquares = 0;
            foreach (var sample in data.Samples)
            {
                var abs = Math.Abs(sample);
                if (abs > data.Peak) data.Peak = abs;
                sumSquares += sample * sample;
            }
            data.RMS = (float)Math.Sqrt(sumSquares / data.Samples.Count);

            return data;
        }

        private static WaveformData ParseWavFile(string audioPath, int sampleCount)
        {
            var data = new WaveformData();

            using var fs = new FileStream(audioPath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            // RIFFヘッダー確認
            var riff = new string(reader.ReadChars(4));
            if (riff != "RIFF") return GeneratePlaceholderWaveform(sampleCount);

            reader.ReadUInt32(); // ファイルサイズ
            var wave = new string(reader.ReadChars(4));
            if (wave != "WAVE") return GeneratePlaceholderWaveform(sampleCount);

            // fmt チャンクを探す
            bool foundFmt = false;
            while (fs.Position < fs.Length - 8)
            {
                var chunkId = new string(reader.ReadChars(4));
                var chunkSize = reader.ReadUInt32();

                if (chunkId == "fmt ")
                {
                    foundFmt = true;
                    var audioFormat = reader.ReadUInt16();
                    var numChannels = reader.ReadUInt16();
                    var sampleRate = reader.ReadUInt32();
                    reader.ReadUInt32(); // byte rate
                    reader.ReadUInt16(); // block align
                    var bitsPerSample = reader.ReadUInt16();

                    if (audioFormat != 1) // PCM以外
                    {
                        return GeneratePlaceholderWaveform(sampleCount);
                    }

                    // 次のチャンクに進む
                    if (chunkSize > 16)
                    {
                        fs.Seek(chunkSize - 16, SeekOrigin.Current);
                    }
                    break;
                }
                else
                {
                    // 不明なチャンクをスキップ
                    fs.Seek(chunkSize, SeekOrigin.Current);
                }
            }

            if (!foundFmt) return GeneratePlaceholderWaveform(sampleCount);

            // dataチャンクを探す
            while (fs.Position < fs.Length - 8)
            {
                var chunkId = new string(reader.ReadChars(4));
                var chunkSize = reader.ReadUInt32();

                if (chunkId == "data")
                {
                    // サンプルデータ読み込み
                    var sampleData = reader.ReadBytes((int)chunkSize);
                    ProcessSampleData(sampleData, data, sampleCount);
                    break;
                }
                else
                {
                    fs.Seek(chunkSize, SeekOrigin.Current);
                }
            }

            if (data.Samples.Count == 0)
            {
                return GeneratePlaceholderWaveform(sampleCount);
            }

            // 統計計算
            data.Peak = 0;
            float sumSquares = 0;
            foreach (var sample in data.Samples)
            {
                var abs = Math.Abs(sample);
                if (abs > data.Peak) data.Peak = abs;
                sumSquares += sample * sample;
            }
            data.RMS = (float)Math.Sqrt(sumSquares / data.Samples.Count);

            return data;
        }

        private static void ProcessSampleData(byte[] sampleData, WaveformData data, int targetCount)
        {
            if (sampleData.Length < 2) return;

            // 16-bit PCMとして解析
            var samples = new List<float>();
            for (int i = 0; i < sampleData.Length - 1; i += 2)
            {
                var sample = (short)((sampleData[i + 1] << 8) | sampleData[i]);
                samples.Add(Math.Abs(sample / 32768.0f)); // 絶対値で包絡線
            }

            if (samples.Count == 0) return;

            // 目標サンプル数にダウンサンプリング
            var downsampled = DownsampleToTarget(samples, targetCount);
            data.Samples.AddRange(downsampled.Samples);
            data.Peak = downsampled.Peak;
            data.RMS = downsampled.RMS;
        }

        /// <summary>
        /// プレースホルダー波形データを生成（解析失敗時用）
        /// </summary>
        private static WaveformData GeneratePlaceholderWaveform(int sampleCount)
        {
            var data = new WaveformData();
            var random = new Random();

            for (int i = 0; i < sampleCount; i++)
            {
                // 疑似적인波形（音声ブロック用）
                var value = (float)(random.NextDouble() * 0.5 + 0.2);
                data.Samples.Add(value);
            }

            data.Peak = 0.7f;
            data.RMS = 0.4f;

            return data;
        }

        /// <summary>
        /// 波形データをPathGeometryに変換（描画用）
        /// </summary>
        public static PathGeometry ConvertToGeometry(WaveformData? data, double width, double height)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure();

            if (data == null || data.Samples.Count == 0)
            {
                return geometry;
            }

            var centerY = height / 2;
            var stepX = width / (data.Samples.Count - 1);

            figure.StartPoint = new Point(0, centerY);
            figure.IsFilled = false;

            var segments = new PathSegmentCollection();

            for (int i = 0; i < data.Samples.Count; i++)
            {
                var x = i * stepX;
                var y = centerY - (data.Samples[i] * centerY * 0.9);
                segments.Add(new LineSegment(new Point(x, y), true));
            }

            for (int i = data.Samples.Count - 1; i >= 0; i--)
            {
                var x = i * stepX;
                var y = centerY + (data.Samples[i] * centerY * 0.9);
                segments.Add(new LineSegment(new Point(x, y), true));
            }

            figure.Segments = segments;
            figure.IsClosed = true;

            geometry.Figures.Add(figure);

            return geometry;
        }
    }
}
