# BigRedProf.Content

The **BigRedProf.Content** library is a content-addressable data store. Content stores store
codes by multihash and record their durable history in a story.

## Glossary

**content** - a **code** stored in, or fetched from, a content store

**multihash** - the self-describing hash that identifies content; a multihash always
identifies the same bits, forever

---

**content store** - the actor that stores content by multihash and records its durable
history in a story; every content store supports export, restore, and tape backup as
invariants, not optional capabilities

**content source** - the read side of content-addressable storage; any place content can be
fetched from by multihash: a full-fledged content store, a client-side cache, or a
read-through chain of sources

**storage provider** - the dumb hash-addressed blob storage behind a content store; owns
only the mechanics of storing and fetching bytes (memory, disk, Azure, S3)

---

**catalog** - the story in which a content store records its history; each successful put
appends a **ContentStored** event

**catalog scribe** - the scribe, bound to the catalog story, that a content store records
its events through

## Invariants

The `ContentStore` class owns the invariants every content store must uphold:

1. **Content identity.** A multihash is computed from the content's bits via
   `Multihash.FromCode`. Byte-aligned content hashes as its raw bytes.
2. **Verification on read.** Fetched content is re-hashed and verified before being
   returned. Corruption throws `ContentIntegrityException`; it is never returned.
3. **Idempotent puts.** Putting the same content twice is safe and returns the same
   multihash.
4. **Cataloging.** Every successful put is recorded in the catalog story. Duplicate
   `ContentStored` events are allowed by design (they preserve clean failure/retry
   behavior); catalog projections deduplicate by multihash at replay time.
5. **Ordering.** The blob is stored first, the catalog event appended second, and the
   multihash returned only after both succeed. A failure in between leaves an orphan blob
   (collectible garbage), never a cataloged-but-missing one — so no external reference can
   ever point at content that export/restore wouldn't recover.

## Usage

```csharp
IContentStoreStorageProvider storageProvider = new MemoryContentStoreStorageProvider();
IScribe catalogScribe = /* a scribe bound to this store's catalog story */;

IContentStore contentStore = new ContentStore(storageProvider, catalogScribe);

Multihash multihash = await contentStore.PutContentAsync(content);
Code? fetchedContent = await contentStore.TryGetContentAsync(multihash);
```

The API boundary is `Code`, not model, so `ContentStore` manages its own pied piper
internally; callers never need to prepare one. Consumers that decode catalog events
themselves (projections, inspection tools) register `ContentStoredPackRat` with their own
pied piper.

Consumers that only read content should depend on `IContentSource`, the read side of
`IContentStore`, so they can be composed with caches and other lightweight sources that
don't carry the durability obligations of a true content store.

## Development

This repository is driven by [Task](https://taskfile.dev). Install it once per
machine:

```powershell
choco install go-task
```

Then, from the repository root:

```powershell
task --list      # see available tasks
task verify      # build + unit tests — everything required before merging
task build       # fast inner loop
task doctor      # toolchain/version diagnostics
task pack        # build the NuGet package locally
```

Task loads the layered environment (`.env.local` then `.env`) on every
invocation, so no shell setup is required — commands work in a fresh process for
humans and agents alike. Note the solution lives at `src/Content.sln`, not at
the repository root.

`BigRedProf.Content.Core` is published to GitHub Packages by CI on a push to
`main`. `task pack` builds the package locally and deliberately cannot push, so
nothing local can release a package by accident. The workflow calls
`task verify` for its build-and-test half, so CI and local agree on what "it
builds" means.

There is no container image here — this repository ships a library, so there is
no `image` or `publish` task. See [script/README.md](script/README.md) for the
(short) script layer.

## Roadmap

* `DiskContentStoreStorageProvider`, `AzureBlobContentStoreStorageProvider`,
  `S3ContentStoreStorageProvider`
* a catalog projection for rebuilding store inventory from the catalog story
* `BigRedProf.Content.Cli` with operations like `content put`, `content get`,
  `content inspect`, `content verify`, `content export`, and `content restore`
* a standard content manifest convention for chunking large media into bounded blobs
* `ContentDestroyed` tombstone events

## License

BigRedProf.Content is licensed under the MIT License. See LICENSE for more information.

## Contact

For questions, suggestions, or issues, please contact Professor at [BigRedProf@outlook.com](BigRedProf@outlook.com).
