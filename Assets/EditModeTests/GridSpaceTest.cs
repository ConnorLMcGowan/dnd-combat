using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using DnDCombat.Core;
using DnDCombat.Unity;

public class GridSpaceTest
{
    private static readonly Vector3EqualityComparer Near = new Vector3EqualityComparer(0.001f);

    ///////////////////////////////////
    // ---------- ToWorld ---------- //
    ///////////////////////////////////

    [Test]
    public void ToWorld_GivesTheCentreOfTheCell()
    {
        GridSpace space = new GridSpace(Vector3.zero, 1f);

        Vector3 result = space.ToWorld(new GridPosition(2, 0, 3));

        Assert.That(result, Is.EqualTo(new Vector3(2.5f, 0.5f, 3.5f)).Using(Near));
    }
}