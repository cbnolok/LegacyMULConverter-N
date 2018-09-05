# LegacyMULConverter-N
Convert (some of the) Ultima Online Classic Client UOP files to MUL and vice versa.<br>
This is an updated version of LegacyMULConverter. Yes, the code is a mess, but i have preferred to update this messy
codebase rather than writing a new one.<br>
Differences with the original version:
* Works with gumpartLegacyMUL.uop as of Classic Client version 7.0.59.5.
* Unpacks and repacks also MultiCollection.uop and multi.mul/multiidx.mul.
 WARNING: since MultiCollection.uop contains an additional file named "housing.bin", which has an unknown content, the repack option
  needs it into the working folder to include it in the uop. It won't be automatically generated. To obtain it, it's sufficient to
  unpack a vanilla MultiCollection.uop.
  I suggest to work on and repack an updated multi.mul, since it's possible that housing.bin contains informations about some
  multis that aren't included in the uop if you repack an old multi.mul. In that case, there's the probability that the client
  will crash when accessing to that file.
  Last warning: the repacker works only with a multi.mul and multiidx.mul with Stygian Abyss format. If you try to repack an older
  multi file, like one from Mondain's Legacy, the client will crash. If you have old multi files, you need to update them to the new format.
