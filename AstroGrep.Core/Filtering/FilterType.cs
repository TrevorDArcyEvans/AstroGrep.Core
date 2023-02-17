namespace AstroGrep.Core;

using System;
using System.Collections.Generic;

/// <summary>
/// Used to define each FilterItem's type is and what it can do.
/// </summary>
/// <remarks>
///   AstroGrep File Searching Utility. Written by Theodore L. Ward
///   Copyright (C) 2002 AstroComma Incorporated.
///   
///   This program is free software; you can redistribute it and/or
///   modify it under the terms of the GNU General Public License
///   as published by the Free Software Foundation; either version 2
///   of the License, or (at your option) any later version.
///   
///   This program is distributed in the hope that it will be useful,
///   but WITHOUT ANY WARRANTY; without even the implied warranty of
///   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
///   GNU General Public License for more details.
///   
///   You should have received a copy of the GNU General Public License
///   along with this program; if not, write to the Free Software
///   Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
/// 
///   The author may be contacted at:
///   ted@astrocomma.com or curtismbeard@gmail.com
/// </remarks>
/// <history>
/// </history>
public class FilterType
{
  private const char DELIMITER = '^';

  /// <summary>
  /// The main types for a FilterItem.
  /// </summary>
  public enum Categories
  {
    File,
    Directory
  }

  /// <summary>
  /// The types of categories for a FilterItem's main type.
  /// </summary>
  public enum SubCategories
  {
    Name,
    Path,
    Hidden,
    System,
    DateModified,
    DateCreated,
    Extension,
    Size,
    Binary,
    MinimumHitCount,
    ReadOnly
  }

  /// <summary>
  /// The object types that are supported for a value.
  /// </summary>
  public enum ValueTypes
  {
    Null,
    String,
    DateTime,
    Long,
    Size
  }

  /// <summary>
  /// The options that can be applied against a value.
  /// </summary>
  public enum ValueOptions
  {
    None,
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanEquals,
    LessThan,
    LessThanEquals
  }

  /// <summary>
  /// Gets the category.
  /// </summary>
  public Categories Category { get; } = Categories.File;

  /// <summary>
  /// Gets the sub category.
  /// </summary>
  public SubCategories SubCategory { get; } = SubCategories.Name;

  /// <summary>
  /// Gets the selected ValueType (type of object the value can be).
  /// </summary>
  public ValueTypes ValueType { get; } = ValueTypes.Null;

  /// <summary>
  /// Gets the list of supported ValueOptions (the options that can be applied against the value).
  /// </summary>
  public List<ValueOptions> SupportedValueOptions { get; } = null;

  /// <summary>
  /// Gets whether this object can support the ignore case directive.
  /// </summary>
  public bool SupportsIgnoreCase { get; } = false;

  /// <summary>
  /// Gets whether this object can support having more than 1 instance of this object defined system wide.
  /// </summary>
  public bool SupportsMultipleItems { get; } = true;

  /// <summary>
  /// Creates an instance of this class with the required category and sub category.
  /// </summary>
  /// <param name="category">Selected category</param>
  /// <param name="subCategory">Selected sub category</param>
  /// <history>
  /// [Curtis_Beard]	   10/31/2014	ADD: exclusions update
  /// </history>
  public FilterType(Categories category, SubCategories subCategory)
  {
    Category = category;
    SubCategory = subCategory;

    switch (SubCategory)
    {
      case SubCategories.Name:
      case SubCategories.Path:
        ValueType = ValueTypes.String;
        SupportedValueOptions = new List<ValueOptions> { ValueOptions.Equals, ValueOptions.Contains, ValueOptions.StartsWith, ValueOptions.EndsWith };
        SupportsIgnoreCase = true;
        SupportsMultipleItems = true;
        break;

      case SubCategories.Hidden:
      case SubCategories.System:
      case SubCategories.ReadOnly:
      case SubCategories.Binary:
        ValueType = ValueTypes.Null;
        SupportedValueOptions = new List<ValueOptions> { ValueOptions.None };
        SupportsIgnoreCase = false;
        SupportsMultipleItems = false;
        break;

      case SubCategories.DateModified:
      case SubCategories.DateCreated:
        ValueType = ValueTypes.DateTime;
        SupportedValueOptions = new List<ValueOptions> { ValueOptions.Equals, ValueOptions.NotEquals, ValueOptions.GreaterThan, ValueOptions.GreaterThanEquals, ValueOptions.LessThan, ValueOptions.LessThanEquals };
        SupportsIgnoreCase = false;
        SupportsMultipleItems = true;
        break;

      case SubCategories.Extension:
        ValueType = ValueTypes.String;
        SupportedValueOptions = new List<ValueOptions> { ValueOptions.None };
        SupportsIgnoreCase = false;
        SupportsMultipleItems = true;
        break;

      case SubCategories.Size:
        ValueType = ValueTypes.Size;
        SupportedValueOptions = new List<ValueOptions> { ValueOptions.Equals, ValueOptions.NotEquals, ValueOptions.GreaterThan, ValueOptions.GreaterThanEquals, ValueOptions.LessThan, ValueOptions.LessThanEquals };
        SupportsIgnoreCase = false;
        SupportsMultipleItems = true;
        break;

      case SubCategories.MinimumHitCount:
        ValueType = ValueTypes.Long;
        SupportedValueOptions = new List<ValueOptions> { ValueOptions.None };
        SupportsIgnoreCase = false;
        SupportsMultipleItems = false;
        break;
    }
  }

  /// <summary>
  /// Converts this class to its string form.
  /// </summary>
  /// <returns>string representation of this class</returns>
  /// <history>
  /// [Curtis_Beard]	   10/31/2014	ADD: exclusions update
  /// </history>
  public override string ToString()
  {
    return string.Format("{1}{0}{2}", DELIMITER, Category.ToString(), SubCategory.ToString());
  }

  /// <summary>
  /// Converts string form of this class to an actual FilterType.
  /// </summary>
  /// <param name="value">string form of a FilterType</param>
  /// <returns>FilterType object</returns>
  /// <history>
  /// [Curtis_Beard]	   10/31/2014	ADD: exclusions update
  /// </history>
  public static FilterType FromString(string value)
  {
    var values = value.Split(DELIMITER);
    var cat = (Categories)Enum.Parse(typeof(Categories), values[0]);
    var subcat = (SubCategories)Enum.Parse(typeof(SubCategories), values[1]);

    return new FilterType(cat, subcat);
  }

  /// <summary>
  /// Creates the default FilterTypes that are available for user selection.
  /// </summary>
  /// <returns>List of FilterTypes that are available for selection</returns>
  /// <history>
  /// [Curtis_Beard]	   10/31/2014	ADD: exclusions update
  /// </history>
  public static List<FilterType> GetDefaultFilterTypes()
  {
    var defaultTypes = new List<FilterType>
    {
      // File
      new FilterType(Categories.File, SubCategories.Extension),
      new FilterType(Categories.File, SubCategories.Name),
      new FilterType(Categories.File, SubCategories.Path),
      new FilterType(Categories.File, SubCategories.System),
      new FilterType(Categories.File, SubCategories.Hidden),
      new FilterType(Categories.File, SubCategories.Binary),
      new FilterType(Categories.File, SubCategories.ReadOnly),
      new FilterType(Categories.File, SubCategories.DateModified),
      new FilterType(Categories.File, SubCategories.DateCreated),
      new FilterType(Categories.File, SubCategories.Size),
      new FilterType(Categories.File, SubCategories.MinimumHitCount),

      // Directory
      new FilterType(Categories.Directory, SubCategories.Name),
      new FilterType(Categories.Directory, SubCategories.Path),
      new FilterType(Categories.Directory, SubCategories.System),
      new FilterType(Categories.Directory, SubCategories.Hidden),
      new FilterType(Categories.Directory, SubCategories.DateModified),
      new FilterType(Categories.Directory, SubCategories.DateCreated)
    };

    return defaultTypes;
  }
}
