﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Google.Common.Geometry
{
    /**
  * Normalizes the cell union by discarding cells that are contained by other
  * cells, replacing groups of 4 child cells by their parent cell whenever
  * possible, and sorting all the cell ids in increasing order. Returns true if
  * the number of cells was reduced.
  *
  *  This method *must* be called before doing any calculations on the cell
  * union, such as Intersects() or Contains().
  *
  * @return true if the normalize operation had any effect on the cell union,
  *         false if the union was already normalized
  */
    public class S2CellUnion : IS2Region, IEnumerable<S2CellId>, IEquatable<S2CellUnion>
    {
        public bool Equals(S2CellUnion other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _cellIds.SequenceEqual(other._cellIds);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((S2CellUnion)obj);
        }

        public override int GetHashCode()
        {
            int value = 17;
            foreach (S2CellId id in this)
            {
                value = 37*value + id.GetHashCode();
            }
            return value;
        }

        public static bool operator ==(S2CellUnion left, S2CellUnion right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(S2CellUnion left, S2CellUnion right)
        {
            return !Equals(left, right);
        }

        /** The CellIds that form the Union */
  private List<S2CellId> _cellIds = new List<S2CellId>();

  public S2CellUnion() {
  }

  public void initFromCellIds(List<S2CellId> cellIds) {
    initRawCellIds(cellIds);
    normalize();
  }

  /**
   * Populates a cell union with the given S2CellIds or 64-bit cells ids, and
   * then calls Normalize(). The InitSwap() version takes ownership of the
   * vector data without copying and clears the given vector. These methods may
   * be called multiple times.
   */
  public void initFromIds(List<ulong> cellIds) {
    initRawIds(cellIds);
    normalize();
  }

  public void initSwap(List<S2CellId> cellIds) {
    initRawSwap(cellIds);
    normalize();
  }

  public void initRawCellIds(List<S2CellId> cellIds) {
    this._cellIds = cellIds;
  }

  public void initRawIds(List<ulong> cellIds) {
    int size = cellIds.Count;
    this._cellIds = new List<S2CellId>(size);
    foreach (ulong id in cellIds) {
      this._cellIds.Add(new S2CellId(id));
    }
  }

  /**
   * Like Init(), but does not call Normalize(). The cell union *must* be
   * normalized before doing any calculations with it, so it is the caller's
   * responsibility to make sure that the input is normalized. This method is
   * useful when converting cell unions to another representation and back.
   * These methods may be called multiple times.
   */
  public void initRawSwap(List<S2CellId> cellIds) {
    this._cellIds = new List<S2CellId>(cellIds);
    cellIds.Clear();
  }

  public int size() {
    return _cellIds.Count;
  }

  /** Convenience methods for accessing the individual cell ids. */
  public S2CellId cellId(int i) {
    return _cellIds[i];
  }

  /** Direct access to the underlying vector for iteration . */
  public List<S2CellId> cellIds() {
    return _cellIds;
  }

  /**
   * Replaces "output" with an expanded version of the cell union where any
   * cells whose level is less than "min_level" or where (level - min_level) is
   * not a multiple of "level_mod" are replaced by their children, until either
   * both of these conditions are satisfied or the maximum level is reached.
   *
   *  This method allows a covering generated by S2RegionCoverer using
   * min_level() or level_mod() constraints to be stored as a normalized cell
   * union (which allows various geometric computations to be done) and then
   * converted back to the original list of cell ids that satisfies the desired
   * constraints.
   */
  public void denormalize(int minLevel, int levelMod, List<S2CellId> output) {
    // assert (minLevel >= 0 && minLevel <= S2CellId.MAX_LEVEL);
    // assert (levelMod >= 1 && levelMod <= 3);

    output.Clear();
    foreach (S2CellId id in this) {
      int level = id.level();
      int newLevel = Math.Max(minLevel, level);
      if (levelMod > 1) {
        // Round up so that (new_level - min_level) is a multiple of level_mod.
        // (Note that S2CellId::kMaxLevel is a multiple of 1, 2, and 3.)
        newLevel += (S2CellId.MAX_LEVEL - (newLevel - minLevel)) % levelMod;
        newLevel = Math.Min(S2CellId.MAX_LEVEL, newLevel);
      }
      if (newLevel == level) {
        output.Add(id);
      } else {
        S2CellId end = id.childEnd(newLevel);
        for (var idInner = id.childBegin(newLevel); !idInner.Equals(end); idInner = idInner.next()) {
          output.Add(idInner);
        }
      }
    }
  }

  /**
   * If there are more than "excess" elements of the cell_ids() vector that are
   * allocated but unused, reallocate the array to eliminate the excess space.
   * This reduces memory usage when many cell unions need to be held in memory
   * at once.
   */
  public void pack() {
    _cellIds.TrimExcess();
  }


  /**
   * Return true if the cell union contains the given cell id. Containment is
   * defined with respect to regions, e.g. a cell contains its 4 children. This
   * is a fast operation (logarithmic in the size of the cell union).
   */
  public bool contains(S2CellId id) {
    // This function requires that Normalize has been called first.
    //
    // This is an exact test. Each cell occupies a linear span of the S2
    // space-filling curve, and the cell id is simply the position at the center
    // of this span. The cell union ids are sorted in increasing order along
    // the space-filling curve. So we simply find the pair of cell ids that
    // surround the given cell id (using binary search). There is containment
    // if and only if one of these two cell ids contains this cell.

    int pos = _cellIds.BinarySearch(id);
    if (pos < 0) {
      pos = -pos - 1;
    }
    if (pos < _cellIds.Count && _cellIds[pos].rangeMin().lessOrEquals(id)) {
      return true;
    }
    return pos != 0 && _cellIds[pos - 1].rangeMax().greaterOrEquals(id);
  }

  /**
   * Return true if the cell union intersects the given cell id. This is a fast
   * operation (logarithmic in the size of the cell union).
   */
  public bool intersects(S2CellId id) {
    // This function requires that Normalize has been called first.
    // This is an exact test; see the comments for Contains() above.
    int pos = _cellIds.BinarySearch(id);

    if (pos < 0) {
      pos = -pos - 1;
    }


    if (pos < _cellIds.Count && _cellIds[pos].rangeMin().lessOrEquals(id.rangeMax())) {
      return true;
    }
    return pos != 0 && _cellIds[pos - 1].rangeMax().greaterOrEquals(id.rangeMin());
  }

  public bool contains(S2CellUnion that) {
    // TODO(kirilll?): A divide-and-conquer or alternating-skip-search approach
    // may be significantly faster in both the average and worst case.
    foreach (S2CellId id in that) {
      if (!this.contains(id)) {
        return false;
      }
    }
    return true;
  }

  /** This is a fast operation (logarithmic in the size of the cell union). */
  public bool contains(S2Cell cell) {
    return contains(cell.id());
  }

  /**
   * Return true if this cell union contain/intersects the given other cell
   * union.
   */
  public bool intersects(S2CellUnion union) {
    // TODO(kirilll?): A divide-and-conquer or alternating-skip-search approach
    // may be significantly faster in both the average and worst case.
    foreach (S2CellId id in union) {
      if (intersects(id)) {
        return true;
      }
    }
    return false;
  }

  public void getUnion(S2CellUnion x, S2CellUnion y) {
    // assert (x != this && y != this);
    _cellIds.Clear();
    
    _cellIds.AddRange(x._cellIds);
    _cellIds.AddRange(y._cellIds);
    normalize();
  }

  /**
   * Specialized version of GetIntersection() that gets the intersection of a
   * cell union with the given cell id. This can be useful for "splitting" a
   * cell union into chunks.
   */
  public void getIntersection(S2CellUnion x, S2CellId id) {
    // assert (x != this);
    _cellIds.Clear();
    if (x.contains(id)) {
      _cellIds.Add(id);
    } else {
      int pos = x._cellIds.BinarySearch(id.rangeMin());

      if (pos < 0) {
        pos = -pos - 1;
      }

      S2CellId idmax = id.rangeMax();
      int size = x._cellIds.Count;
      while (pos < size && x._cellIds[pos].lessOrEquals(idmax)) {
        _cellIds.Add(x._cellIds[pos++]);
      }
    }
  }

  /**
   * Initialize this cell union to the union or intersection of the two given
   * cell unions. Requires: x != this and y != this.
   */
  public void getIntersection(S2CellUnion x, S2CellUnion y) {
    // assert (x != this && y != this);

    // This is a fairly efficient calculation that uses binary search to skip
    // over sections of both input vectors. It takes constant time if all the
    // cells of "x" come before or after all the cells of "y" in S2CellId order.

    _cellIds.Clear();

    int i = 0;
    int j = 0;

    while (i < x._cellIds.Count && j < y._cellIds.Count) {
      S2CellId imin = x.cellId(i).rangeMin();
      S2CellId jmin = y.cellId(j).rangeMin();
      if (imin.greaterThan(jmin)) {
        // Either j->contains(*i) or the two cells are disjoint.
        if (x.cellId(i).lessOrEquals(y.cellId(j).rangeMax())) {
          _cellIds.Add(x.cellId(i++));
        } else {
          // Advance "j" to the first cell possibly contained by *i.
          j = indexedBinarySearch(y._cellIds, imin, j + 1);
          // The previous cell *(j-1) may now contain *i.
          if (x.cellId(i).lessOrEquals(y.cellId(j - 1).rangeMax())) {
            --j;
          }
        }
      } else if (jmin.greaterThan(imin)) {
        // Identical to the code above with "i" and "j" reversed.
        if (y.cellId(j).lessOrEquals(x.cellId(i).rangeMax())) {
          _cellIds.Add(y.cellId(j++));
        } else {
          i = indexedBinarySearch(x._cellIds, jmin, i + 1);
          if (y.cellId(j).lessOrEquals(x.cellId(i - 1).rangeMax())) {
            --i;
          }
        }
      } else {
        // "i" and "j" have the same range_min(), so one contains the other.
        if (x.cellId(i).lessThan(y.cellId(j))) {
          _cellIds.Add(x.cellId(i++));
        } else {
          _cellIds.Add(y.cellId(j++));
        }
      }
    }
    // The output is generated in sorted order, and there should not be any
    // cells that can be merged (provided that both inputs were normalized).
    // assert (!normalize());
  }

  /**
   * Just as normal binary search, except that it allows specifying the starting
   * value for the lower bound.
   *
   * @return The position of the searched element in the list (if found), or the
   *         position where the element could be inserted without violating the
   *         order.
   */
  private int indexedBinarySearch(List<S2CellId> l, S2CellId key, int low) {
    int high = l.Count - 1;

    while (low <= high) {
      int mid = (low + high) >> 1;
      S2CellId midVal = l[mid];
      int cmp = midVal.CompareTo(key);

      if (cmp < 0) {
        low = mid + 1;
      } else if (cmp > 0) {
        high = mid - 1;
      } else {
        return mid; // key found
      }
    }
    return low; // key not found
  }

  /**
   * Expands the cell union such that it contains all cells of the given level
   * that are adjacent to any cell of the original union. Two cells are defined
   * as adjacent if their boundaries have any points in common, i.e. most cells
   * have 8 adjacent cells (not counting the cell itself).
   *
   *  Note that the size of the output is exponential in "level". For example,
   * if level == 20 and the input has a cell at level 10, there will be on the
   * order of 4000 adjacent cells in the output. For most applications the
   * Expand(min_fraction, min_distance) method below is easier to use.
   */
  public void expand(int level) {
    List<S2CellId> output = new List<S2CellId>();
    ulong levelLsb = S2CellId.lowestOnBitForLevel(level);
    int i = size() - 1;
    do {
      S2CellId id = cellId(i);
      if (id.lowestOnBit() < levelLsb) {
        id = id.parent(level);
        // Optimization: skip over any cells contained by this one. This is
        // especially important when very small regions are being expanded.
        while (i > 0 && id.contains(cellId(i - 1))) {
          --i;
        }
      }
      output.Add(id);
      id.getAllNeighbors(level, output);
    } while (--i >= 0);
    initSwap(output);
  }

  /**
   * Expand the cell union such that it contains all points whose distance to
   * the cell union is at most minRadius, but do not use cells that are more
   * than maxLevelDiff levels higher than the largest cell in the input. The
   * second parameter controls the tradeoff between accuracy and output size
   * when a large region is being expanded by a small amount (e.g. expanding
   * Canada by 1km).
   *
   *  For example, if maxLevelDiff == 4, the region will always be expanded by
   * approximately 1/16 the width of its largest cell. Note that in the worst
   * case, the number of cells in the output can be up to 4 * (1 + 2 **
   * maxLevelDiff) times larger than the number of cells in the input.
   */
  public void expand(S1Angle minRadius, int maxLevelDiff) {
    int minLevel = S2CellId.MAX_LEVEL;
    foreach (S2CellId id in this) {
      minLevel = Math.Min(minLevel, id.level());
    }
    // Find the maximum level such that all cells are at least "min_radius"
    // wide.
    int radiusLevel = S2Projections.MIN_WIDTH.getMaxLevel(minRadius.radians());
    if (radiusLevel == 0 && minRadius.radians() > S2Projections.MIN_WIDTH.getValue(0)) {
      // The requested expansion is greater than the width of a face cell.
      // The easiest way to handle this is to expand twice.
      expand(0);
    }
    expand(Math.Min(minLevel + maxLevelDiff, radiusLevel));
  }

  
  public IS2Region clone() {
    S2CellUnion copy = new S2CellUnion();
    copy.initRawCellIds(new List<S2CellId>(_cellIds));
    return copy;
  }
        
  public S2Cap getCapBound() {
    // Compute the approximate centroid of the region. This won't produce the
    // bounding cap of minimal area, but it should be close enough.
    if (_cellIds.Count == 0) {
      return S2Cap.empty();
    }
    S2Point centroid = new S2Point(0, 0, 0);
    foreach (S2CellId id in this) {
      double area = S2Cell.averageArea(id.level());
      centroid = S2Point.add(centroid, S2Point.mul(id.toPoint(), area));
    }
    if (centroid.Equals(new S2Point(0, 0, 0))) {
      centroid = new S2Point(1, 0, 0);
    } else {
      centroid = S2Point.normalize(centroid);
    }

    // Use the centroid as the cap axis, and expand the cap angle so that it
    // contains the bounding caps of all the individual cells. Note that it is
    // *not* sufficient to just bound all the cell vertices because the bounding
    // cap may be concave (i.e. cover more than one hemisphere).
    S2Cap cap = S2Cap.fromAxisHeight(centroid, 0);
    foreach (S2CellId id in this) {
      cap = cap.addCap(new S2Cell(id).getCapBound());
    }
    return cap;
  }
        
  public S2LatLngRect getRectBound() {
    S2LatLngRect bound = S2LatLngRect.empty();
    foreach (S2CellId id in this) {
      bound = bound.union(new S2Cell(id).getRectBound());
    }
    return bound;
  }


  /** This is a fast operation (logarithmic in the size of the cell union). */
  public bool mayIntersect(S2Cell cell) {
    return intersects(cell.id());
  }

  /**
   * The point 'p' does not need to be normalized. This is a fast operation
   * (logarithmic in the size of the cell union).
   */
  public bool contains(S2Point p) {
    return contains(S2CellId.fromPoint(p));

  }

  /**
   * The number of leaf cells covered by the union.
   * This will be no more than 6*2^60 for the whole sphere.
   *
   * @return the number of leaf cells covered by the union
   */
  public long leafCellsCovered() {
    long numLeaves = 0;
    foreach (S2CellId cellId in _cellIds) {
      int invertedLevel = S2CellId.MAX_LEVEL - cellId.level();
      numLeaves += (1L << (invertedLevel << 1));
    }
    return numLeaves;
  }


  /**
   * Approximate this cell union's area by summing the average area of
   * each contained cell's average area, using {@link S2Cell#averageArea()}.
   * This is equivalent to the number of leaves covered, multiplied by
   * the average area of a leaf.
   * Note that {@link S2Cell#averageArea()} does not take into account
   * distortion of cell, and thus may be off by up to a factor of 1.7.
   * NOTE: Since this is proportional to LeafCellsCovered(), it is
   * always better to use the other function if all you care about is
   * the relative average area between objects.
   *
   * @return the sum of the average area of each contained cell's average area
   */
  public double averageBasedArea() {
    return S2Cell.averageArea(S2CellId.MAX_LEVEL) * leafCellsCovered();
  }

  /**
   * Calculates this cell union's area by summing the approximate area for each
   * contained cell, using {@link S2Cell#approxArea()}.
   *
   * @return approximate area of the cell union
   */
  public double approxArea() {
    double area = 0;
    foreach (S2CellId cellId in _cellIds) {
      area += new S2Cell(cellId).approxArea();
    }
    return area;
  }

  /**
   * Calculates this cell union's area by summing the exact area for each
   * contained cell, using the {@link S2Cell#exactArea()}.
   *
   * @return the exact area of the cell union
   */
  public double exactArea() {
    double area = 0;
    foreach (S2CellId cellId in _cellIds) {
      area += new S2Cell(cellId).exactArea();
    }
    return area;
  }




  /**
   * Normalizes the cell union by discarding cells that are contained by other
   * cells, replacing groups of 4 child cells by their parent cell whenever
   * possible, and sorting all the cell ids in increasing order. Returns true if
   * the number of cells was reduced.
   *
   *  This method *must* be called before doing any calculations on the cell
   * union, such as Intersects() or Contains().
   *
   * @return true if the normalize operation had any effect on the cell union,
   *         false if the union was already normalized
   */
  public bool normalize() {
    // Optimize the representation by looking for cases where all subcells
    // of a parent cell are present.

    List<S2CellId> output = new List<S2CellId>(_cellIds.Count);
      _cellIds.Sort();
    

    foreach (S2CellId idLoop in this)
    {
        var id = idLoop;
      int sze = output.Count;
      // Check whether this cell is contained by the previous cell.
      if (output.Any() && output[sze - 1].contains(id)) {
        continue;
      }

      // Discard any previous cells contained by this cell.
      while (output.Any() && id.contains(output[output.Count - 1])) {
        output.RemoveAt(output.Count - 1);
      }

      // Check whether the last 3 elements of "output" plus "id" can be
      // collapsed into a single parent cell.
      while (output.Count >= 3) {
        sze = output.Count;
        // A necessary (but not sufficient) condition is that the XOR of the
        // four cells must be zero. This is also very fast to test.
        if ((output[sze - 3].id() ^ output[sze - 2].id() ^ output[sze - 1].id())
            != id.id()) {
          break;
        }

        // Now we do a slightly more expensive but exact test. First, compute a
        // mask that blocks out the two bits that encode the child position of
        // "id" with respect to its parent, then check that the other three
        // children all agree with "mask.
        ulong mask = id.lowestOnBit() << 1;
        mask = ~(mask + (mask << 1));
        ulong idMasked = (id.id() & mask);
        if ((output[sze - 3].id() & mask) != idMasked
            || (output[sze - 2].id() & mask) != idMasked
            || (output[sze - 1].id() & mask) != idMasked || id.isFace()) {
          break;
        }

        // Replace four children by their parent cell.
        output.RemoveAt(sze - 1);
        output.RemoveAt(sze - 2);
        output.RemoveAt(sze - 3);
        id = id.parent();
      }
      output.Add(id);
    }
    if (output.Count < size()) {
      initRawSwap(output);
      return true;
    }
    return false;
  }

        public IEnumerator<S2CellId> GetEnumerator()
        {
            return _cellIds.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}