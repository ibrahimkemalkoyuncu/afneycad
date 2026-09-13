using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: BCF Dışa Aktarım Servisi (BcfExportService)
   NEDEN — Session #75 iş akışı denetiminde bulunan boşluk: `ClashReportDialog` sadece CSV
          dışa aktarabiliyordu. BCF (BIM Collaboration Format), çakışma/yorum bulgularını
          Solibri/BIMcollab/Navisworks gibi araçlar arasında değiştirmek için endüstri
          standardıdır (buildingSMART) — CSV'nin aksine, her bulgu için başlık/öncelik/durum
          ve bir kamera konumu (viewpoint) taşıyan, standart bir ZIP paketidir.
   KAPSAM/DÜRÜSTLÜK NOTU: AfneyCAD'in entity kimlikleri IFC GUID'i DEĞİLDİR (AfneyCAD IFC-native
          bir modelleyici değil) — bu yüzden BCF konuları, hedef araçta doğrudan seçilebilir bir
          IFC bileşen referansı TAŞIMIYOR. Her konu; başlık, öncelik, çakışma konumunda bir
          kamera görüş noktası (viewpoint) ve AfneyCAD'in kendi entity ID'lerini içeren bir
          yorum (Comment) taşıyor — bu, izlenebilirlik için yeterli ama "tıkla-vurgula" IFC
          entegrasyonu değil. Format buildingSMART BCF 2.1 şemasının basitleştirilmiş, geçerli
          bir alt kümesidir (bcf.version + konu başına markup.bcf + viewpoint.bcfv).
*/
public static class BcfExportService
{
    public static void Export(IEnumerable<ClashResult> clashes, string filePath)
    {
        if (File.Exists(filePath)) File.Delete(filePath);

        using var zip = ZipFile.Open(filePath, ZipArchiveMode.Create);

        WriteVersionFile(zip);

        foreach (var clash in clashes)
        {
            string topicGuid = clash.Id.ToString();
            string folder = topicGuid + "/";

            WriteMarkup(zip, folder, clash);
            WriteViewpoint(zip, folder, clash);
        }
    }

    private static void WriteVersionFile(ZipArchive zip)
    {
        var entry = zip.CreateEntry("bcf.version");
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<Version VersionId=\"2.1\" xmlns=\"http://www.buildingsmart-tech.org/specifications/BCF-Version\">\n" +
            "  <DetailedVersion>2.1</DetailedVersion>\n" +
            "</Version>\n");
    }

    private static void WriteMarkup(ZipArchive zip, string folder, ClashResult clash)
    {
        var entry = zip.CreateEntry(folder + "markup.bcf");
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);

        string priority = clash.Severity == ClashSeverity.Critical ? "Critical" : "Warning";
        string status = clash.IsApproved ? "Resolved" : "Open";
        string title = Escape(clash.Message);
        string comment = Escape(
            $"AfneyCAD entity: {clash.EntityA_Id}" +
            (clash.EntityB_Id.HasValue ? $" ↔ {clash.EntityB_Id}" : "") +
            (clash.ObstacleId.HasValue ? $" (engel: {clash.ObstacleId})" : "") +
            " — bu ID'ler AfneyCAD'in kendi iç kimlikleridir, IFC GUID değildir.");
        string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        writer.Write(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<Markup xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
            $"  <Topic Guid=\"{clash.Id}\" TopicType=\"Clash\" TopicStatus=\"{status}\">\n" +
            $"    <Title>{title}</Title>\n" +
            $"    <Priority>{priority}</Priority>\n" +
            $"    <CreationDate>{now}</CreationDate>\n" +
            "    <CreationAuthor>AfneyCAD</CreationAuthor>\n" +
            "  </Topic>\n" +
            $"  <Comment Guid=\"{Guid.NewGuid()}\">\n" +
            $"    <Date>{now}</Date>\n" +
            "    <Author>AfneyCAD</Author>\n" +
            $"    <Comment>{comment}</Comment>\n" +
            "  </Comment>\n" +
            "  <Viewpoints Guid=\"" + Guid.NewGuid() + "\" Viewpoint=\"viewpoint.bcfv\"/>\n" +
            "</Markup>\n");
    }

    private static void WriteViewpoint(ZipArchive zip, string folder, ClashResult clash)
    {
        var entry = zip.CreateEntry(folder + "viewpoint.bcfv");
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);

        // Kamerayı çakışma noktasının 3m yukarısından, aşağı bakacak şekilde konumlandırıyoruz —
        // gerçek bir sahne kamerası değil (AfneyCAD BCF görüntü render etmiyor), sadece hedef
        // aracın kamerayı doğru koordinata götürmesi için makul bir varsayılan.
        string x = clash.Position.X.ToString("F2", CultureInfo.InvariantCulture);
        string y = clash.Position.Y.ToString("F2", CultureInfo.InvariantCulture);
        string z = (clash.Position.Z + 3000).ToString("F2", CultureInfo.InvariantCulture);

        writer.Write(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            $"<VisualizationInfo Guid=\"{Guid.NewGuid()}\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
            "  <PerspectiveCamera>\n" +
            $"    <CameraViewPoint><X>{x}</X><Y>{y}</Y><Z>{z}</Z></CameraViewPoint>\n" +
            "    <CameraDirection><X>0</X><Y>0</Y><Z>-1</Z></CameraDirection>\n" +
            "    <CameraUpVector><X>0</X><Y>1</Y><Z>0</Z></CameraUpVector>\n" +
            "    <FieldOfView>60</FieldOfView>\n" +
            "  </PerspectiveCamera>\n" +
            "</VisualizationInfo>\n");
    }

    private static string Escape(string s) => System.Security.SecurityElement.Escape(s) ?? s;
}
