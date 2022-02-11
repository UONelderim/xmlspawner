# XmlSpawner2 for ServUO publish 58

This repo is a copy of https://code.google.com/archive/p/xmlspawner/source with changes required to Plug&Play on ServUO pub 58. 

Plan is to keep it working with ServUO p58 (or maybe even later versions) and possibly do a cleanup of the code base.

# How to install

1. Remove original files of XmlSpawner from ServUO (_Scripts/Services/XmlSpawner_)
2. Put files from this repository somewhere in Scripts directory
3. in file **Scripts/Misc/ItemFlags.cs** replace:
>public static class ItemFlags

with

>public static partial class ItemFlags
