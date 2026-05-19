using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Xunit;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Tests.Mechanical
{
    public class ValidationGateTests
    {
        [Fact]
        public void ValidateSystem_WithOpenPipe_ShouldReturnError()
        {
            // Arrange
            var db = new CadDatabase();
            var kernel = new Afney.Cad.Mechanical.MechanicalKernel();
            kernel.SetDatabase(db);

            // Sadece bir boru ekle (uçları boş)
            var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 20);
            db.AddEntity(pipe);
            kernel.OnEntityAddedToDatabase(pipe);

            // Act
            var result = kernel.ValidationGate.CheckGateBeforeCalculation(out var validationResult);

            // Assert
            Assert.False(result);
            Assert.Contains(validationResult.Errors, e => e.Contains("Açık Uç"));
        }

        [Fact]
        public void ValidateSystem_WithNoFixtures_ShouldReturnV_P01Error()
        {
            // Arrange
            var db = new CadDatabase();
            var kernel = new Afney.Cad.Mechanical.MechanicalKernel();
            kernel.SetDatabase(db);

            // Act
            kernel.ValidationGate.CheckGateBeforeCalculation(out var validationResult);

            // Assert
            Assert.Contains(validationResult.Errors, e => e.Contains("V-P01"));
        }

        [Fact]
        public void ExcelExport_ShouldCreateFile()
        {
            // Arrange
            var bomService = new BOMExportService();
            var entities = new List<Afney.Cad.Domain.Abstractions.CadEntity>
            {
                new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 25) { SystemType = MechanicalSystemType.DomesticColdWater },
                new SanitaryFixtureEntity(new Vector3D(500, 500, 0), "Lavabo", 0.5)
            };
            string tempFile = Path.Combine(Path.GetTempPath(), $"AfneyTest_{Guid.NewGuid()}.xlsx");

            try
            {
                // Act
                bomService.GenerateExcelReport(entities, "Test Proje", tempFile);

                // Assert
                Assert.True(File.Exists(tempFile));
                var fileInfo = new FileInfo(tempFile);
                Assert.True(fileInfo.Length > 0);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
