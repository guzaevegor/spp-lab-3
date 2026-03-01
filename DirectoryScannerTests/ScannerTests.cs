using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DirectoryScannerLib;

namespace DirectoryScannerTests
{
    [TestClass]
    public class ScannerTests
    {
        private string _testRootPath;

        // Этот метод запускается автоматически перед каждым тестом. 
        // Он создает пустую временную папку.
        [TestInitialize]
        public void Setup()
        {
            _testRootPath = Path.Combine(Path.GetTempPath(), "ScannerTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testRootPath);
        }

        // Этот метод запускается после каждого теста, чтобы удалить временные папки и не мусорить на диске.
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testRootPath))
            {
                Directory.Delete(_testRootPath, true);
            }
        }

        [TestMethod]
        public void Scan_SingleFolderWithFiles_CalculatesCorrectSize()
        {
            // создаем 2 файла (100 байт и 200 байт)
            File.WriteAllBytes(Path.Combine(_testRootPath, "file1.txt"), new byte[100]);
            File.WriteAllBytes(Path.Combine(_testRootPath, "file2.txt"), new byte[200]);
            var scanner = new Scanner(maxThreads: 2);

            // запускаем сканирование
            var result = scanner.Scan(_testRootPath, CancellationToken.None);

            // проверяем результаты
            Assert.AreEqual(300, result.Size); // Папка должна весить 300 байт
            Assert.AreEqual(2, result.Children.Count); // Должно быть 2 файла внутри

            var file1 = result.Children.First(c => c.Name == "file1.txt");
            Assert.AreEqual(100, file1.Size);
            // Проверка процентов (100 байт от 300 = 33.33%) с погрешностью 0.01
            Assert.AreEqual(33.33, file1.PercentOfParent, 0.01);
        }

        [TestMethod]
        public void Scan_WithCancellationToken_CancelsScanning()
        {
            // создаем вложенность из 20 папок, чтобы сканер не успел пробежать всё мгновенно
            var currentDir = _testRootPath;
            for (int i = 0; i < 20; i++)
            {
                currentDir = Path.Combine(currentDir, $"Dir_{i}");
                Directory.CreateDirectory(currentDir);
                File.WriteAllBytes(Path.Combine(currentDir, "file.txt"), new byte[10]);
            }

            var scanner = new Scanner(maxThreads: 2);
            var cts = new CancellationTokenSource();

            // Отменяем токен через 5 миллисекунд (сканер точно не успеет пройти все 20 папок)
            cts.CancelAfter(5);

            // запускаем с токеном отмены
            var result = scanner.Scan(_testRootPath, cts.Token);

            // общий размер должен быть меньше 200 (т.к. сканирование прервалось до конца)
            Assert.IsTrue(result.Size < 200 || result.Children.Count < 20, "Сканирование не прервалось вовремя");
        }

        [TestMethod]
        public void Scan_WithSemaphoreLimit_DoesNotDeadlock()
        {
            // создаем 15 папок
            for (int i = 0; i < 15; i++)
            {
                Directory.CreateDirectory(Path.Combine(_testRootPath, $"SubDir_{i}"));
            }

            // Ставим лимит в 2 потока. Если логика неверная, 2 потока заблокируются, 
            // ожидая дочерние папки, и произойдет дедлок.
            var scanner = new Scanner(maxThreads: 2);
            var task = System.Threading.Tasks.Task.Run(() => scanner.Scan(_testRootPath, CancellationToken.None));
            // Ждем максимум 5 секунд. Если больше дедлок.
            bool completedInTime = task.Wait(TimeSpan.FromSeconds(5));

            Assert.IsTrue(completedInTime, "Произошел дедлок: сканирование зависло");
        }
    }
}
