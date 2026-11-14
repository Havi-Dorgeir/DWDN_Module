using System;

namespace DWDN_MechanicsTester.Suites
{
    public enum ArtilleryState
    {
        InTransportCart, // В кузове походной тележки на конной упряжи
        Deploying,       // Процесс расцепки
        DeployedCombat   // Развернуто в боевое положение со складом снарядов
    }

    public class MockArtilleryTrain
    {
        public const float HitchDistance = 2.8f;
        public const float MaxWalkSpeed = 1.8f; // Максимальная скорость шага (м/с)
        public const float DeployInteractRadius = 3.0f;

        public ArtilleryState State { get; set; } = ArtilleryState.InTransportCart;
        public string EngineType { get; set; } = "ballista"; // "ballista" или "mangonel"

        // Лошадь
        public float HorsePosX { get; set; } = 0f;
        public float HorsePosY { get; set; } = 0f;
        public float HorseHeadingRad { get; set; } = 0f; // 0 = восток (+X), pi/2 = север (+Y)
        public float HorseSpeed { get; set; } = 0f;
        public bool HorseSpeedLimited { get; set; } = true;

        // Тележка
        public float CartPosX { get; set; } = -HitchDistance;
        public float CartPosY { get; set; } = 0f;
        public float CartHeadingRad { get; set; } = 0f;

        // Боевое орудие и склад
        public bool SiegeWeaponActive { get; set; } = false;
        public bool AmmoPileSpawned { get; set; } = false;
        public int AmmoCount { get; set; } = 0;
        public float AmmoPilePosX { get; set; }
        public float AmmoPilePosY { get; set; }

        public void StepMovement(float dt, float desiredSpeed, float turnRateRad)
        {
            if (State != ArtilleryState.InTransportCart) return;

            // 1. Ограничение скорости: конь может идти строго шагом!
            float allowedSpeed = HorseSpeedLimited ? Math.Min(desiredSpeed, MaxWalkSpeed) : desiredSpeed;
            HorseSpeed = allowedSpeed;

            // 2. Движение коня
            HorseHeadingRad += turnRateRad * dt;
            float fwdX = (float)Math.Cos(HorseHeadingRad);
            float fwdY = (float)Math.Sin(HorseHeadingRad);

            HorsePosX += fwdX * HorseSpeed * dt;
            HorsePosY += fwdY * HorseSpeed * dt;

            // 3. Расчет позиции тележки за конем на расстоянии HitchDistance
            float targetCartX = HorsePosX - fwdX * HitchDistance;
            float targetCartY = HorsePosY - fwdY * HitchDistance;

            // Сглаженное следование тележки (дымшло тяги)
            CartPosX = targetCartX;
            CartPosY = targetCartY;
            CartHeadingRad = HorseHeadingRad;
        }

        public bool TryDeploy(float playerX, float playerY)
        {
            if (State != ArtilleryState.InTransportCart) return false;

            float dx = playerX - CartPosX;
            float dy = playerY - CartPosY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            // Проверяем радиус взаимодействия [F]
            if (dist > DeployInteractRadius)
                return false;

            // Обряд расцепки:
            State = ArtilleryState.DeployedCombat;
            HorseSpeedLimited = false; // Конь свободен
            SiegeWeaponActive = true;  // Орудие перешло в боевой нативный режим

            // Спавн склада снарядов рядом с орудием (на 1.5м вправо)
            float rightX = -(float)Math.Sin(CartHeadingRad);
            float rightY = (float)Math.Cos(CartHeadingRad);

            AmmoPilePosX = CartPosX + rightX * 1.5f;
            AmmoPilePosY = CartPosY + rightY * 1.5f;
            AmmoPileSpawned = true;
            AmmoCount = (EngineType == "ballista") ? 20 : 25;

            return true;
        }
    }

    public static class ArtilleryTrainTestSuite
    {
        public static TestSuiteResult Run()
        {
            var suite = new TestSuiteResult
            {
                SuiteName = "Horse-Drawn Artillery Limber & Deployment Mechanics",
                Description = "Тестирование походной упряжки, ограничения шага коня, следования тележки bd_cart_a и боевой расцепки [F]"
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();

            RunTest(suite, "Test_Horse_Speed_Limited_To_Walk_Only", () =>
            {
                var train = new MockArtilleryTrain();

                // Игрок пытается разогнаться галопом до 7.5 м/с
                train.StepMovement(1.0f, desiredSpeed: 7.5f, turnRateRad: 0f);

                Assert.IsTrue(train.HorseSpeedLimited, "Ограничитель скорости упряжного коня должен быть активен");
                Assert.AreEqual(MockArtilleryTrain.MaxWalkSpeed, train.HorseSpeed, "Скорость коня в упряжке должна быть строго ограничена шагом (1.8 м/с)");
            });

            RunTest(suite, "Test_Cart_Trailing_Follows_Horse_Smoothly", () =>
            {
                var train = new MockArtilleryTrain();

                // Конь шагает вперед на 10 секунд со скоростью 1.5 м/с
                for (int i = 0; i < 10; i++)
                {
                    train.StepMovement(1.0f, desiredSpeed: 1.5f, turnRateRad: 0f);
                }

                // Конь прошел 15 метров
                Assert.AreApproximatelyEqual(15.0f, train.HorsePosX, 0.05f, "Конь должен пройти 15 метров");
                // Тележка должна быть ровно на 2.8 метра позади коня
                float expectedCartX = 15.0f - MockArtilleryTrain.HitchDistance;
                Assert.AreApproximatelyEqual(expectedCartX, train.CartPosX, 0.05f, "Тележка обязана точно следовать за конем на дистанции сцепки 2.8м");
            });

            RunTest(suite, "Test_Deployment_Distance_Check", () =>
            {
                var train = new MockArtilleryTrain();

                // Игрок стоит слишком далеко (10 метров) и жмет [F]
                bool deployedFar = train.TryDeploy(playerX: 10.0f, playerY: 0f);
                Assert.IsFalse(deployedFar, "Нельзя расцепить орудие, находясь дальше радиуса взаимодействия (3м)");
                Assert.AreEqual(ArtilleryState.InTransportCart, train.State, "Орудие должно оставаться в походном виде");

                // Игрок подходит вплотную (1.5м) и жмет [F]
                bool deployedClose = train.TryDeploy(playerX: train.CartPosX + 1.0f, playerY: train.CartPosY);
                Assert.IsTrue(deployedClose, "В радиусе взаимодействия [F] расцепка должна успешно сработать");
                Assert.AreEqual(ArtilleryState.DeployedCombat, train.State, "Орудие должно перейти в боевой режим DeployedCombat");
            });

            RunTest(suite, "Test_Deployment_Spawns_Ammo_And_Frees_Horse", () =>
            {
                var train = new MockArtilleryTrain { EngineType = "ballista" };

                // Расцепляем у тележки
                train.TryDeploy(playerX: train.CartPosX, playerY: train.CartPosY);

                Assert.IsTrue(train.SiegeWeaponActive, "Боевое осадное орудие должно активироваться");
                Assert.IsFalse(train.HorseSpeedLimited, "После расцепки конь освобождается от скоростного лимита");
                Assert.IsTrue(train.AmmoPileSpawned, "Склад боеприпасов должен заспавниться рядом с орудием");
                Assert.AreEqual(20, train.AmmoCount, "Для баллисты должно заспавниться 20 болтов");

                // Проверяем, что склад находится на расстоянии ~1.5м от лафета
                float dx = train.AmmoPilePosX - train.CartPosX;
                float dy = train.AmmoPilePosY - train.CartPosY;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                Assert.AreApproximatelyEqual(1.5f, dist, 0.05f, "Склад снарядов должен располагаться в 1.5м справа от лафета для удобства расчёта");
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
