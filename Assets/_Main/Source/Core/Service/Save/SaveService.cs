using System;
using System.IO;
using UnityEngine;

namespace CasualKit.Core
{
    public sealed class SaveService : Service, ISaveService, ILateTickable
    {
        private const int FirstLevelIndex = 0;
        private const int CurrentSaveVersion = 3;
        public const string DefaultFileName = "save.json";
        public const string TestFileName = "save.test.json";

        private readonly string m_Path;
        private readonly string m_TempPath;
        private readonly string m_BackupPath;
        private readonly ITimeService m_Time;
        private SaveData m_Data;
        private bool m_Dirty;

        public static string FilePath => Path.Combine(Application.persistentDataPath, DefaultFileName);

        public static void DeleteSaveFiles()
        {
            DeleteSaveFiles(DefaultFileName);
        }

        public static void DeleteSaveFiles(string fileName)
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            TryDeleteFile(path);
            TryDeleteFile(path + ".bak");
            TryDeleteFile(path + ".tmp");
        }

        private static void TryDeleteFile(string path)
        {
            if (!File.Exists(path))
                return;

            File.Delete(path);
        }

        public SaveService() : this(DefaultFileName, null)
        {
        }

        public SaveService(string fileName, ITimeService time)
        {
            if (string.IsNullOrEmpty(fileName))
                fileName = DefaultFileName;

            m_Path = Path.Combine(Application.persistentDataPath, fileName);
            m_TempPath = m_Path + ".tmp";
            m_BackupPath = m_Path + ".bak";
            m_Time = time ?? new SystemTimeService();
        }

        private CoreProgressData Progress => m_Data.Progress;

        public int CurrentLevelIndex => Progress.CurrentLevelIndex;
        public int CurrentLevelNumber => Progress.CurrentLevelIndex + 1;
        public bool HasCompletedFirstLevel => Progress.FirstLevelCompleted;
        public string GameJson => m_Data.GameJson;

        public int GetLevelScore(int levelIndex)
        {
            LevelRecordData[] scores = Progress.LevelScores;
            if (scores == null)
                return 0;

            for (int i = 0; i < scores.Length; i++)
            {
                if (scores[i].LevelIndex == levelIndex)
                    return scores[i].Score;
            }

            return 0;
        }

        public int GetTotalScore() => Progress.TotalScore;
        public int GetTotalAttempts() => Progress.TotalAttempts;
        public int GetTotalCompletionSeconds() => Progress.TotalCompletionSeconds;

        public int GetLevelAttempts(int levelIndex)
        {
            LevelRecordData record = FindLevelRecord(levelIndex);
            return record != null ? record.Attempts : 0;
        }

        public void CompleteLevel(int levelIndex, int score, int completionSeconds)
        {
            UpsertScore(levelIndex, score, completionSeconds);

            if (levelIndex == FirstLevelIndex)
                Progress.FirstLevelCompleted = true;

            if (levelIndex >= Progress.CurrentLevelIndex)
                Progress.CurrentLevelIndex = levelIndex + 1;

            Progress.TotalCompletionSeconds += completionSeconds;
            Progress.TotalScore += score;
            MarkDirty();
        }

        public void SetGameJson(string json)
        {
            m_Data.GameJson = json ?? string.Empty;
            MarkDirty();
        }

        public void FlushPending()
        {
            PersistIfDirty();
        }

        public void LateTick(float deltaTime)
        {
            PersistIfDirty();
        }

        public int IncrementLevelAttempts(int levelIndex)
        {
            LevelRecordData record = FindOrCreateLevelRecord(levelIndex);
            record.Attempts++;
            Progress.TotalAttempts++;
            MarkDirty();
            return record.Attempts;
        }

        protected override void OnInitialize()
        {
            m_Data = Load();
            Application.quitting += OnApplicationQuitting;
            Application.focusChanged += OnApplicationFocusChanged;
        }

        protected override void OnDispose()
        {
            Application.quitting -= OnApplicationQuitting;
            Application.focusChanged -= OnApplicationFocusChanged;
            PersistIfDirty();
        }

        private void OnApplicationQuitting()
        {
            PersistIfDirty();
        }

        private void OnApplicationFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
                PersistIfDirty();
        }

        private void UpsertScore(int levelIndex, int score, int completionSeconds)
        {
            LevelRecordData record = FindOrCreateLevelRecord(levelIndex);
            if (score > record.Score)
                record.Score = score;
            if (completionSeconds > record.CompletionSeconds)
                record.CompletionSeconds = completionSeconds;
        }

        private LevelRecordData FindLevelRecord(int levelIndex)
        {
            LevelRecordData[] scores = Progress.LevelScores;
            if (scores == null)
                return null;

            for (int i = 0; i < scores.Length; i++)
            {
                if (scores[i].LevelIndex == levelIndex)
                    return scores[i];
            }

            return null;
        }

        private LevelRecordData FindOrCreateLevelRecord(int levelIndex)
        {
            LevelRecordData existing = FindLevelRecord(levelIndex);
            if (existing != null)
                return existing;

            LevelRecordData created = new LevelRecordData
            {
                LevelIndex = levelIndex,
                Score = 0,
                Attempts = 0,
                CompletionSeconds = 0
            };

            LevelRecordData[] scores = Progress.LevelScores;
            if (scores == null || scores.Length == 0)
            {
                Progress.LevelScores = new[] { created };
                return created;
            }

            LevelRecordData[] expanded = new LevelRecordData[scores.Length + 1];
            Array.Copy(scores, expanded, scores.Length);
            expanded[scores.Length] = created;
            Progress.LevelScores = expanded;
            return created;
        }

        private SaveData Load()
        {
            SaveData data = TryRead(m_Path);
            if (data == null)
            {
                data = TryRead(m_BackupPath);
                if (data != null)
                    EditorLog.Warning("Save file unreadable, recovered from backup.");
            }

            return data != null ? data : CreateDefault();
        }

        private static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                LegacySaveData legacy = JsonUtility.FromJson<LegacySaveData>(json);
                if (legacy == null)
                    return null;

                if (legacy.Version >= 2 && legacy.Progress != null)
                {
                    SaveData nested = JsonUtility.FromJson<SaveData>(json);
                    return EnsureShape(nested);
                }

                return MigrateLegacy(legacy);
            }
            catch (Exception exception)
            {
                EditorLog.Error("Save read failed at " + path + ": " + exception.Message);
                return null;
            }
        }

        private static SaveData MigrateLegacy(LegacySaveData legacy)
        {
            return EnsureShape(new SaveData
            {
                Version = CurrentSaveVersion,
                Progress = new CoreProgressData
                {
                    CurrentLevelIndex = legacy.CurrentLevelIndex,
                    FirstLevelCompleted = legacy.FirstLevelCompleted,
                    TotalScore = legacy.TotalScore,
                    TotalAttempts = legacy.TotalAttempts,
                    TotalCompletionSeconds = legacy.TotalCompletionSeconds,
                    LevelScores = legacy.LevelScores ?? Array.Empty<LevelRecordData>()
                },
                GameJson = string.Empty
            });
        }

        private static SaveData EnsureShape(SaveData data)
        {
            if (data == null)
                return CreateDefault();

            if (data.Progress == null)
                data.Progress = new CoreProgressData();
            if (data.Progress.LevelScores == null)
                data.Progress.LevelScores = Array.Empty<LevelRecordData>();
            if (data.GameJson == null)
                data.GameJson = string.Empty;
            if (data.Version < CurrentSaveVersion)
                data.Version = CurrentSaveVersion;

            return data;
        }

        private void MarkDirty()
        {
            m_Dirty = true;
        }

        private void PersistIfDirty()
        {
            if (!m_Dirty || m_Data == null)
                return;

            try
            {
                File.WriteAllText(m_TempPath, JsonUtility.ToJson(m_Data));
                if (File.Exists(m_Path))
                    File.Replace(m_TempPath, m_Path, m_BackupPath);
                else
                    File.Move(m_TempPath, m_Path);

                m_Dirty = false;
            }
            catch (Exception exception)
            {
                EditorLog.Error("Save write failed: " + exception.Message);
            }
        }

        private static SaveData CreateDefault()
        {
            return EnsureShape(new SaveData
            {
                Version = CurrentSaveVersion,
                Progress = new CoreProgressData
                {
                    CurrentLevelIndex = FirstLevelIndex,
                    FirstLevelCompleted = false,
                    LevelScores = Array.Empty<LevelRecordData>()
                },
                GameJson = string.Empty
            });
        }
    }
}
