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
IPiedPiper piedPiper = new PiedPiper();
IContentStoreStorageProvider storageProvider = new MemoryContentStoreStorageProvider();
IScribe catalogScribe = /* a scribe bound to this store's catalog story */;

IContentStore contentStore = new ContentStore(piedPiper, storageProvider, catalogScribe);

Multihash multihash = await contentStore.PutContentAsync(content);
Code? fetchedContent = await contentStore.TryGetContentAsync(multihash);
```

Consumers that only read content should depend on `IContentSource`, the read side of
`IContentStore`, so they can be composed with caches and other lightweight sources that
don't carry the durability obligations of a true content store.

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
