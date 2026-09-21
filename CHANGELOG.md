# Changelog

## 1.1.3

### Added
- Three new sequential quests: **Supply Interruption**, **Black Ledger**, and **Priority Shipment**.
- High-end quest unlocks spread across six stages, including premium armor, helmets, complete Western rifles, and restricted AP ammunition.
- Kill objectives for Scavs on Customs and PMCs.
- Startup logging that prints the number of purchase unlocks attached to every quest.

### Fixed
- Quest image field now points to a valid registered image instead of an empty string.
- Quest-assort status keys now use the lowercase values expected by SPT (`started`, `success`, `fail`).
- Project, metadata, build scripts, and release naming synchronized to version 1.1.3.

### Needs validation
- A community report says Quartermaster's assort may fail to load after returning from a raid. The quest-assort key mismatch was corrected in this build and may be related, but the post-raid behavior should be reproduced and verified before marking it fully resolved.
- Manual quest acceptance should be retested on a normal/fresh profile. Existing profiles may retain quest state from older builds.
