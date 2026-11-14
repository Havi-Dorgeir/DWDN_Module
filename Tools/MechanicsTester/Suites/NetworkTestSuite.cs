using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DWDN_MechanicsTester.Suites
{
    /// <summary>
    /// Эмулятор бинарного сериализатора GameNetwork TaleWorlds
    /// </summary>
    public class MockNetworkStream
    {
        private readonly MemoryStream _ms = new();
        private BinaryWriter? _writer;
        private BinaryReader? _reader;

        public MockNetworkStream()
        {
            _writer = new BinaryWriter(_ms, Encoding.UTF8, true);
        }

        public void WriteInt(int value) => _writer!.Write(value);
        public void WriteFloat(float value) => _writer!.Write(value);
        public void WriteBool(bool value) => _writer!.Write(value);
        public void WriteString(string value) => _writer!.Write(value ?? "");

        public void ResetForReading()
        {
            _ms.Position = 0;
            _reader = new BinaryReader(_ms, Encoding.UTF8, true);
        }

        public int ReadInt(ref bool valid)
        {
            try { return _reader!.ReadInt32(); }
            catch { valid = false; return 0; }
        }

        public float ReadFloat(ref bool valid)
        {
            try { return _reader!.ReadSingle(); }
            catch { valid = false; return 0f; }
        }

        public bool ReadBool(ref bool valid)
        {
            try { return _reader!.ReadBoolean(); }
            catch { valid = false; return false; }
        }

        public string ReadString(ref bool valid)
        {
            try { return _reader!.ReadString(); }
            catch { valid = false; return ""; }
        }

        public long Position => _ms.Position;
        public long Length => _ms.Length;
    }

    public static class NetworkTestSuite
    {
        public static TestSuiteResult Run()
        {
            var suite = new TestSuiteResult
            {
                SuiteName = "Network Protocol & Packet Serialization",
                Description = "Тестирование побайтовой сериализации/десериализации сетевых пакетов DWDN"
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();

            RunTest(suite, "Test_RequestCustomLoadout_Serialization_Roundtrip", () =>
            {
                var stream = new MockNetworkStream();
                bool valid = true;

                // Запись пакета
                string duchyId = "banreich";
                string classId = "mp_Artillerist";
                string helmet = "dwdn_pikeman_kettlehad";
                string body = "Banobranny_surcoat";
                string legs = "banobranyi_leg";
                string gloves = "crossbowman_mailgoves";
                string cape = "archer_scarf";
                string slot0 = "dwdn_falchion";
                string slot1 = "dwdn_blueprint_ballista";
                string slot2 = "";
                string slot3 = "";
                string horse = "";

                stream.WriteString(duchyId);
                stream.WriteString(classId);
                stream.WriteString(helmet);
                stream.WriteString(body);
                stream.WriteString(legs);
                stream.WriteString(gloves);
                stream.WriteString(cape);
                stream.WriteString(slot0);
                stream.WriteString(slot1);
                stream.WriteString(slot2);
                stream.WriteString(slot3);
                stream.WriteString(horse);

                // Чтение
                stream.ResetForReading();
                string rDuchy = stream.ReadString(ref valid);
                string rClass = stream.ReadString(ref valid);
                string rHelmet = stream.ReadString(ref valid);
                string rBody = stream.ReadString(ref valid);
                string rLegs = stream.ReadString(ref valid);
                string rGloves = stream.ReadString(ref valid);
                string rCape = stream.ReadString(ref valid);
                string rSlot0 = stream.ReadString(ref valid);
                string rSlot1 = stream.ReadString(ref valid);
                string rSlot2 = stream.ReadString(ref valid);
                string rSlot3 = stream.ReadString(ref valid);
                string rHorse = stream.ReadString(ref valid);

                Assert.IsTrue(valid, "Поток данных должен оставаться валидным");
                Assert.AreEqual(duchyId, rDuchy, "DuchyId должен совпадать");
                Assert.AreEqual(classId, rClass, "ClassId должен совпадать");
                Assert.AreEqual(helmet, rHelmet, "HelmetId должен совпадать");
                Assert.AreEqual(body, rBody, "BodyId должен совпадать");
                Assert.AreEqual(slot0, rSlot0, "Slot0 должен совпадать");
                Assert.AreEqual(slot1, rSlot1, "Slot1 (Чертёж баллисты) должен совпадать");
                Assert.AreEqual(stream.Position, stream.Length, "Поток должен быть прочитан полностью");
            });

            RunTest(suite, "Test_DeployArtilleryMessage_Serialization", () =>
            {
                var stream = new MockNetworkStream();
                bool valid = true;

                int cartId = 42;
                string engineType = "ballista";
                float posX = 150.25f;
                float posY = 320.50f;
                float posZ = 12.75f;
                float yaw = 1.57f;

                stream.WriteInt(cartId);
                stream.WriteString(engineType);
                stream.WriteFloat(posX);
                stream.WriteFloat(posY);
                stream.WriteFloat(posZ);
                stream.WriteFloat(yaw);

                stream.ResetForReading();
                int rCartId = stream.ReadInt(ref valid);
                string rEngine = stream.ReadString(ref valid);
                float rX = stream.ReadFloat(ref valid);
                float rY = stream.ReadFloat(ref valid);
                float rZ = stream.ReadFloat(ref valid);
                float rYaw = stream.ReadFloat(ref valid);

                Assert.IsTrue(valid, "Десериализация DeployArtilleryMessage должна быть успешной");
                Assert.AreEqual(cartId, rCartId, "CartId");
                Assert.AreEqual(engineType, rEngine, "EngineType");
                Assert.AreApproximatelyEqual(posX, rX, 0.001f, "PosX");
                Assert.AreApproximatelyEqual(posY, rY, 0.001f, "PosY");
                Assert.AreApproximatelyEqual(posZ, rZ, 0.001f, "PosZ");
                Assert.AreApproximatelyEqual(yaw, rYaw, 0.001f, "Yaw");
            });

            RunTest(suite, "Test_SetArtilleryPushStateMessage_Serialization", () =>
            {
                var stream = new MockNetworkStream();
                bool valid = true;

                int engineId = 7;
                int pusherIndex = 12;
                bool isPushing = true;

                stream.WriteInt(engineId);
                stream.WriteInt(pusherIndex);
                stream.WriteBool(isPushing);

                stream.ResetForReading();
                int rEngine = stream.ReadInt(ref valid);
                int rPusher = stream.ReadInt(ref valid);
                bool rPushing = stream.ReadBool(ref valid);

                Assert.IsTrue(valid, "Поток валиден");
                Assert.AreEqual(engineId, rEngine, "EngineId");
                Assert.AreEqual(pusherIndex, rPusher, "PusherIndex");
                Assert.AreEqual(isPushing, rPushing, "IsPushing");
            });

            RunTest(suite, "Test_Corrupted_Stream_Detection", () =>
            {
                var stream = new MockNetworkStream();
                bool valid = true;

                // Пишем только 1 int, но пытаемся прочитать 3 int
                stream.WriteInt(100);
                stream.ResetForReading();

                stream.ReadInt(ref valid);
                Assert.IsTrue(valid, "Первый int валиден");

                stream.ReadInt(ref valid);
                Assert.IsFalse(valid, "Чтение за пределами потока должно пометить valid=false (защита от сбоя сети)");
            });

            sw.Stop();
            suite.DurationMs = sw.Elapsed.TotalMilliseconds;
            return suite;
        }

        private static void RunTest(TestSuiteResult suite, string testName, Action testAction)
        {
            var tc = new TestCaseResult { Name = testName, SuiteName = suite.SuiteName };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                testAction();
                tc.Status = TestStatus.Passed;
                tc.Message = "PASSED";
            }
            catch (Exception ex)
            {
                tc.Status = TestStatus.Failed;
                tc.Message = ex.Message;
                tc.StackTrace = ex.StackTrace;
            }
            finally
            {
                sw.Stop();
                tc.DurationMs = sw.Elapsed.TotalMilliseconds;
                suite.Results.Add(tc);
            }
        }
    }
}
