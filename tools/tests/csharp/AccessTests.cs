// Unit tests for the accessibility options (Day 11) in Misdirection.Core.Access.
using System;
using System.Numerics;
using Misdirection.Core.Access;
using Misdirection.Core.Crowd;
using Misdirection.Core.Hands;
using Misdirection.Core.Show;
using Misdirection.Core.Table;
using Misdirection.Core.Tricks;

public static class AccessOptionsTests
{
    [Test]
    public static void DefaultsAreInclusiveAndNeverAskForTheMicrophone()
    {
        var o = new AccessOptions();
        Assert.Equal(OneHandMode.Auto, o.OneHand, "one-handed switches itself on");
        Assert.True(o.Captions, "captions on");
        Assert.False(o.Voice, "voice off: no microphone prompt until the player asks");
        Assert.False(o.Relaxed);
        Assert.False(o.HighContrast);
    }

    [Test]
    public static void RoundTripsThroughItsSavedString()
    {
        var o = new AccessOptions { OneHand = OneHandMode.On, Relaxed = true, Captions = false, HighContrast = true, Voice = true };
        string text = o.Encode();
        Assert.Equal("one=on;relaxed=1;captions=0;contrast=1;voice=1", text);
        AccessOptions back = AccessOptions.Decode(text);
        Assert.Equal(text, back.Encode(), "round trip");
    }

    [Test]
    public static void UnreadableSavesFallBackToDefaults()
    {
        string defaults = new AccessOptions().Encode();
        Assert.Equal(defaults, AccessOptions.Decode(null).Encode(), "null");
        Assert.Equal(defaults, AccessOptions.Decode("").Encode(), "empty");
        Assert.Equal(defaults, AccessOptions.Decode("garbage;;=;one=sideways;voice=yes").Encode(), "garbage");
        AccessOptions partial = AccessOptions.Decode("relaxed=1");
        Assert.True(partial.Relaxed, "the readable part is kept");
        Assert.True(partial.Captions, "the rest stays default");
    }

    [Test]
    public static void OneHandCyclesAutoOnOffAndTheRestFlip()
    {
        var o = new AccessOptions();
        o.Toggle(AccessToggle.OneHand);
        Assert.Equal(OneHandMode.On, o.OneHand);
        o.Toggle(AccessToggle.OneHand);
        Assert.Equal(OneHandMode.Off, o.OneHand);
        Assert.False(o.IsOn(AccessToggle.OneHand), "off");
        o.Toggle(AccessToggle.OneHand);
        Assert.Equal(OneHandMode.Auto, o.OneHand);
        Assert.Equal("auto", o.StateWord(AccessToggle.OneHand));
        o.Toggle(AccessToggle.Captions);
        Assert.False(o.Captions);
        o.Toggle(AccessToggle.Voice);
        Assert.True(o.Voice);
        Assert.Equal("on", o.StateWord(AccessToggle.Voice));
        for (int i = 0; i < AccessOptions.ToggleCount; i++)
        {
            Assert.True(AccessOptions.Label((AccessToggle)i).Split(new char[] { ' ' }).Length <= 2, "labels are one or two words");
        }
    }
}
