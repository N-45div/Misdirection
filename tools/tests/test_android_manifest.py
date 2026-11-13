"""Checks the Android manifest never lets the app prompt for a permission by itself at startup.

Unity's Android player asks for every "dangerous" permission in the manifest when the app launches,
unless the manifest carries unityplayer.SkipPermissionsDialog = true. Day 11 adds an optional voice
alternative ("Look!") that needs the microphone, which must only be asked for when the player switches
voice on. Meta's manifest template sets the flag to false, so regenerating the manifest with Meta's
tools would quietly bring the startup prompt back: this test catches that before it ships.
"""

import os
import unittest
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
MANIFEST = os.path.join(REPO, "Assets", "Plugins", "Android", "AndroidManifest.xml")
ANDROID = "{http://schemas.android.com/apk/res/android}"


def _names(root, tag):
    return [element.get(ANDROID + "name") for element in root.findall(tag)]


class AndroidManifest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = ET.parse(MANIFEST).getroot()

    def test_unity_does_not_ask_for_permissions_at_startup(self):
        flags = [m.get(ANDROID + "value") for m in self.root.findall("application/meta-data")
                 if m.get(ANDROID + "name") == "unityplayer.SkipPermissionsDialog"]
        self.assertEqual(flags, ["true"],
                         "unityplayer.SkipPermissionsDialog must be true (run Misdirection > Day 11 > "
                         "Check: No Permission Prompt at Startup)")

    def test_microphone_is_declared_but_optional(self):
        self.assertIn("android.permission.RECORD_AUDIO", _names(self.root, "uses-permission"),
                      "voice \"Look!\" can only ask for the microphone if the manifest declares it")
        microphone = [f for f in self.root.findall("uses-feature")
                      if f.get(ANDROID + "name") == "android.hardware.microphone"]
        self.assertTrue(microphone, "declare the microphone feature")
        self.assertEqual(microphone[0].get(ANDROID + "required"), "false", "the game must install without a microphone")

    def test_room_scan_and_hand_permissions_are_kept(self):
        permissions = _names(self.root, "uses-permission")
        for needed in ("com.oculus.permission.USE_SCENE", "com.oculus.permission.USE_ANCHOR_API",
                       "com.oculus.permission.HAND_TRACKING"):
            self.assertIn(needed, permissions)


if __name__ == "__main__":
    unittest.main()
