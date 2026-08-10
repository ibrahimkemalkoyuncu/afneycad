using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.SpatialIndex.Core;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: QuadTree Remove Erken Çıkış + Merge/Shrink Testleri
   NEDEN: Performans denetiminde Remove()'un bulunduktan sonra bile gereksiz alt-düğüm
          taramasına devam ettiği ve düğüm birleştirme (merge/shrink) hiç yapılmadığı
          ("TODO: Merge (Optimize)") tespit edildi. Bu testler:
          1. Insert/QueryRange/Remove'un temel doğruluğunun korunduğunu (davranış aynı),
          2. Sil-sil-sil sonrası çocuk düğümlerin gerçekten birleşip ağacın yaprak
             (undivided) hâle döndüğünü,
          3. Sınırı aşan (boundary-crossing) bir nesnenin birden fazla çocuğa eklenip
             Remove ile HER ikisinden de düzgünce silindiğini (naif "ilk bulunduğu yerde
             dur" erken-çıkış'ın KIRACAĞI senaryo),
          doğrular.
*/
public class QuadTreeTests
{
    private static CadBoundingBox WorldBounds() =>
        new CadBoundingBox(new Vector3D(-1000, -1000, 0), new Vector3D(1000, 1000, 0));

    private static CircleEntity CreateCircle(double x, double y, double radius = 1)
        => new CircleEntity(new Vector3D(x, y, 0), radius);

    [Fact]
    public void Insert_And_QueryRange_FindsInsertedEntity()
    {
        var tree = new QuadTree(WorldBounds(), capacity: 4);
        var circle = CreateCircle(10, 10);
        tree.Insert(circle);

        var found = new HashSet<CadEntity>();
        tree.QueryRange(new CadBoundingBox(new Vector3D(0, 0, 0), new Vector3D(20, 20, 0)), found);

        Assert.Contains(circle, found);
    }

    [Fact]
    public void Remove_ExistingEntity_RemovesFromQueryResults()
    {
        var tree = new QuadTree(WorldBounds(), capacity: 4);
        var circle = CreateCircle(10, 10);
        tree.Insert(circle);

        bool removed = tree.Remove(circle);

        var found = new HashSet<CadEntity>();
        tree.QueryRange(WorldBounds(), found);

        Assert.True(removed);
        Assert.DoesNotContain(circle, found);
    }

    [Fact]
    public void Remove_NonExistentEntity_ReturnsFalse_AndDoesNotThrow()
    {
        var tree = new QuadTree(WorldBounds(), capacity: 4);
        var circle = CreateCircle(10, 10);

        bool removed = tree.Remove(circle);

        Assert.False(removed);
    }

    [Fact]
    public void ManyInsertsAndRemoves_AllEntitiesQueryableAndRemovable()
    {
        // Ağacı zorlayarak birden çok kez bölünmeye (Subdivide) sokar,
        // sonra hepsini teker teker siler; hem Insert/Subdivide hem de
        // Remove (erken çıkış + merge) doğruluğunu bir arada test eder.
        var tree = new QuadTree(WorldBounds(), capacity: 4);
        var entities = new List<CircleEntity>();

        var rnd = new System.Random(42);
        for (int i = 0; i < 500; i++)
        {
            var e = CreateCircle(rnd.Next(-900, 900), rnd.Next(-900, 900), 0.5);
            entities.Add(e);
            tree.Insert(e);
        }

        var allFound = new HashSet<CadEntity>();
        tree.QueryRange(WorldBounds(), allFound);
        Assert.Equal(entities.Count, allFound.Count);

        foreach (var e in entities)
        {
            bool removed = tree.Remove(e);
            Assert.True(removed, $"Entity {e.Id} silinemedi");
        }

        var remaining = new HashSet<CadEntity>();
        tree.QueryRange(WorldBounds(), remaining);
        Assert.Empty(remaining);
    }

    [Fact]
    public void BoundaryCrossingEntity_RemovedFromAllIntersectingChildren()
    {
        // Küçük bir dünya + düşük kapasite ile ağacı hemen böl, sonra tam sınırda
        // (4 çeyreğin kesişimine yakın) büyük bir bbox'lı nesne ekleyip sil.
        // Bu senaryo, "ilk bulunduğu çocukta dur" tarzı naif bir erken-çıkışın
        // KIRACAĞI durumdur: nesne birden fazla çocuğa eklenmiş olabilir.
        var bounds = new CadBoundingBox(new Vector3D(-100, -100, 0), new Vector3D(100, 100, 0));
        var tree = new QuadTree(bounds, capacity: 2);

        // Kapasiteyi doldurup bölünmeyi tetikle
        tree.Insert(CreateCircle(-50, -50, 1));
        tree.Insert(CreateCircle(50, 50, 1));
        tree.Insert(CreateCircle(-50, 50, 1));

        // Tam merkezde (0,0), 4 çeyreği de kesen büyük bir nesne
        var crossing = CreateCircle(0, 0, 30);
        tree.Insert(crossing);

        var beforeRemove = new HashSet<CadEntity>();
        tree.QueryRange(bounds, beforeRemove);
        Assert.Contains(crossing, beforeRemove);

        bool removed = tree.Remove(crossing);
        Assert.True(removed);

        var afterRemove = new HashSet<CadEntity>();
        tree.QueryRange(bounds, afterRemove);
        Assert.DoesNotContain(crossing, afterRemove);

        // Aynı bölgeyi tekrar sorgulasak bile (farklı alt-bölge sorguları dahil)
        // hayalet referans kalmamalı.
        var nw = new HashSet<CadEntity>();
        tree.QueryRange(new CadBoundingBox(new Vector3D(-100, 0, 0), new Vector3D(0, 100, 0)), nw);
        Assert.DoesNotContain(crossing, nw);

        var se = new HashSet<CadEntity>();
        tree.QueryRange(new CadBoundingBox(new Vector3D(0, -100, 0), new Vector3D(100, 0, 0)), se);
        Assert.DoesNotContain(crossing, se);
    }

    [Fact]
    public void Merge_AfterDeletingMostEntities_TreeCollapsesBackToLeaf()
    {
        // Küçük kapasite ile ağacı zorla böl, sonra çoğu nesneyi sil.
        // Kalan nesne sayısı kapasitenin altına düşünce çocuklar birleşip
        // ağaç tekrar tek bir yaprak düğüme dönmeli (IsDivided == false)
        // ve node sayısı azalmalı.
        var bounds = new CadBoundingBox(new Vector3D(-100, -100, 0), new Vector3D(100, 100, 0));
        var tree = new QuadTree(bounds, capacity: 4);

        var entities = new List<CircleEntity>();
        // Her çeyreğe birkaç nesne koyup kapasiteyi aşarak bölünmeyi tetikle
        double[,] positions =
        {
            { -50, -50 }, { -60, -60 }, { -40, -40 },
            {  50,  50 }, {  60,  60 }, {  40,  40 },
            { -50,  50 }, { -60,  60 }, { -40,  40 },
            {  50, -50 }, {  60, -60 }, {  40, -40 },
        };
        for (int i = 0; i < positions.GetLength(0); i++)
        {
            var e = CreateCircle(positions[i, 0], positions[i, 1], 0.5);
            entities.Add(e);
            tree.Insert(e);
        }

        Assert.True(tree.IsDivided, "Ağaç bu kadar nesneyle bölünmüş olmalıydı");

        // Kapasitenin (4) çok altına inecek şekilde neredeyse hepsini sil
        for (int i = 0; i < entities.Count - 1; i++)
        {
            tree.Remove(entities[i]);
        }

        Assert.False(tree.IsDivided, "Çoğu nesne silindikten sonra ağaç tekrar yaprak olmalı (merge)");

        // Kalan tek nesne hâlâ doğru şekilde sorgulanabilmeli
        var remaining = new HashSet<CadEntity>();
        tree.QueryRange(bounds, remaining);
        Assert.Single(remaining);
        Assert.Contains(entities[^1], remaining);
    }
}
