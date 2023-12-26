//
// System.Configuration.ConfigurationElement.cs
//
// Authors:
//	Duncan Mak (duncan@ximian.com)
// 	Lluis Sanchez Gual (lluis@novell.com)
// 	Martin Baulig <martin.baulig@xamarin.com>
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// Copyright (C) 2004 Novell, Inc (http://www.novell.com)
// Copyright (c) 2012 Xamarin Inc. (http://www.xamarin.com)
//

using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Reflection;
using System.IO;
using System.ComponentModel;

namespace System.Configuration
{
	public abstract class ConfigurationElement
	{
		string rawXml;
		bool modified;
		ConfigurationPropertyCollection keyProps;
		ConfigurationElementCollection defaultCollection;
		bool readOnly;
		ElementInformation elementInfo;
		Configuration _configuration;
		bool elementPresent;

		/* referencesource members. */
                internal static readonly Object s_nullPropertyValue = new Object();
                private static ConfigurationElementProperty s_ElementProperty =
                    new ConfigurationElementProperty(new DefaultValidator());
                private static readonly Hashtable s_propertyBags = new Hashtable();
                private static volatile Dictionary<Type,ConfigurationValidatorBase> s_perTypeValidators;
                private ConfigurationElementProperty    _elementProperty = s_ElementProperty;
                private bool                            _bDataToWrite;
                private readonly ConfigurationValues    _values;
                private bool                            _bInited;
                private string                          _elementTagName;
                internal const string DefaultCollectionPropertyName = "";

		internal Configuration Configuration {
			get { return _configuration; }
			set { _configuration = value; }
		}

		/* From referencesource. */
		internal bool DataToWriteInternal {
                    get {
                        return _bDataToWrite;
                    }
                    set {
                        _bDataToWrite = value;
                    }
                }

                internal ConfigurationElement CreateElement(Type type) {
                        // We use this.GetType() as the calling type since all code paths which lead to
                        // CreateElement are protected methods, so inputs are provided by somebody in
                        // the current type hierarchy. Since we expect that the most subclassed type
                        // will be the most restricted security-wise, we'll use it as the calling type.

                        ConfigurationElement element = (ConfigurationElement)TypeUtil.CreateInstanceRestricted(callingType: GetType(), targetType: type);
                        element.CallInit();
                        return element;
                }

		protected ConfigurationElement ()
		{
                        _values = new ConfigurationValues();

                        // Set the per-type validator ( this will actually have an effect only for an attributed model elements )
                        // Note that in the case where the property bag fot this.GetType() has not yet been created
                        // the validator for this instance will get applied in ApplyValidatorsRecursive ( see this.get_Properties )
                        ApplyValidator(this);
		}

                // Give elements that are added to a collection an opportunity to
                //
                protected internal virtual void Init() {
                    // If Init is called by the derived class, we may be able
                    // to set _bInited to true if the derived class properly
                    // calls Init on its base.
                    _bInited = true;
                }

                internal void CallInit() {
                    // Ensure Init is called just once
                    if (!_bInited) {
                        Init();
                        _bInited = true;
                    }
                }
		
		internal virtual void InitFromProperty (PropertyInformation propertyInfo)
		{
			elementInfo = new ElementInformation (this, propertyInfo);
			Init ();
		}
		
		public ElementInformation ElementInformation {
			get {
				if (elementInfo == null)
					elementInfo = new ElementInformation (this, null);
				return elementInfo;
			}
		}

		internal string RawXml {
			get { return rawXml; }
			set {
				// FIXME: this hack is nasty. We should make
				// some refactory on the entire assembly.
				if (rawXml == null || value != null)
					rawXml = value;
			}
		}

		protected internal virtual ConfigurationElementProperty ElementProperty {
			get {
                                return _elementProperty;
			}
		}

		protected ContextInformation EvaluationContext {
			get {
				if (Configuration != null)
					return Configuration.EvaluationContext;
				throw new ConfigurationErrorsException (
					"This element is not currently associated with any context.");
			}
		}

		ConfigurationLockCollection lockAllAttributesExcept;
		public ConfigurationLockCollection LockAllAttributesExcept {
			get {
				if (lockAllAttributesExcept == null) {
					lockAllAttributesExcept = new ConfigurationLockCollection (this, ConfigurationLockType.Attribute | ConfigurationLockType.Exclude);
				}

				return lockAllAttributesExcept;
			}
		}

		ConfigurationLockCollection lockAllElementsExcept;
		public ConfigurationLockCollection LockAllElementsExcept {
			get {
				if (lockAllElementsExcept == null) {
					lockAllElementsExcept = new ConfigurationLockCollection (this, ConfigurationLockType.Element | ConfigurationLockType.Exclude);
				}

				return lockAllElementsExcept;
			}
		}

		ConfigurationLockCollection lockAttributes;
		public ConfigurationLockCollection LockAttributes {
			get {
				if (lockAttributes == null) {
					lockAttributes = new ConfigurationLockCollection (this, ConfigurationLockType.Attribute);
				}

				return lockAttributes;
			}
		}

		ConfigurationLockCollection lockElements;
		public ConfigurationLockCollection LockElements {
			get {
				if (lockElements == null) {
					lockElements = new ConfigurationLockCollection (this, ConfigurationLockType.Element);
				}

				return lockElements;
			}
		}

		bool lockItem;
		public bool LockItem {
			get { return lockItem; }
			set { lockItem = value; }
		}

		[MonoTODO]
		protected virtual void ListErrors (IList errorList)
		{
			throw new NotImplementedException ();
		}

		/* Pulled from referencesource. */
		protected void SetPropertyValue(ConfigurationProperty prop, object value, bool ignoreLocks) {
		    if (IsReadOnly()) {
			throw new ConfigurationErrorsException(SR.GetString(SR.Config_base_read_only));
		    }

		    /* FIXME:LOCKING: We haven't pulled in referencesource's ConfigurationElement locking code.
		    if ((ignoreLocks == false) &&
			((_lockedAllExceptAttributesList != null && _lockedAllExceptAttributesList.HasParentElements && !_lockedAllExceptAttributesList.DefinedInParent(prop.Name)) ||
			    (_lockedAttributesList != null && (_lockedAttributesList.DefinedInParent(prop.Name) || _lockedAttributesList.DefinedInParent(LockAll))) ||
			    ((_fItemLocked & ConfigurationValueFlags.Locked) != 0) &&
			    (_fItemLocked & ConfigurationValueFlags.Inherited) != 0)) {
			throw new ConfigurationErrorsException(SR.GetString(SR.Config_base_attribute_locked, prop.Name));
		    }
		    */

		    modified = true;

		    // Run the new value through the validator to make sure its ok to store it
		    if (value != null) {
			prop.Validate(value);
		    }

		    _values[prop.Name] = (value != null) ? value : s_nullPropertyValue;
		}

		internal ConfigurationPropertyCollection GetKeyProperties ()
		{
			if (keyProps != null) return keyProps;
			
			ConfigurationPropertyCollection tmpkeyProps = new ConfigurationPropertyCollection ();
				foreach (ConfigurationProperty prop in Properties) {
					if (prop.IsKey)
					tmpkeyProps.Add (prop);
				}

			return keyProps = tmpkeyProps;
		}

		internal ConfigurationElementCollection GetDefaultCollection ()
		{
			if (defaultCollection != null) return defaultCollection;

			ConfigurationProperty defaultCollectionProp = null;

			foreach (ConfigurationProperty prop in Properties) {
				if (prop.IsDefaultCollection) {
					defaultCollectionProp = prop;
					break;
				}
			}

			if (defaultCollectionProp != null) {
				defaultCollection = this [defaultCollectionProp] as ConfigurationElementCollection;
			}

			return defaultCollection;
		}

		protected internal Object this[ConfigurationProperty prop] {
			get {
				Object o = _values[prop.Name];
				if (o == null) {
					if (prop.IsConfigurationElementType) {
						lock (_values.SyncRoot) {
							o = _values[prop.Name];
							if (o == null) {
								ConfigurationElement childElement = CreateElement(prop.Type);

								if (readOnly) {
									childElement.SetReadOnly();
								}

								if (typeof(ConfigurationElementCollection).IsAssignableFrom(prop.Type)) {
									ConfigurationElementCollection childElementCollection = childElement as ConfigurationElementCollection;
									if (prop.AddElementName != null)
										childElementCollection.AddElementName = prop.AddElementName;
									if (prop.RemoveElementName != null)
										childElementCollection.RemoveElementName = prop.RemoveElementName;
									if (prop.ClearElementName != null)
										childElementCollection.ClearElementName = prop.ClearElementName;
								}

								//_values[prop.Name] = childElement;
								_values.SetValue(prop.Name, childElement, ConfigurationValueFlags.Inherited, null);
								o = childElement;
							}
						}
					}
					else {
						o = prop.DefaultValue;
					}
				}
				else if (o == s_nullPropertyValue) {
					o = null;
				}

				// If its an invalid value - throw the error now
				if (o is InvalidPropValue) {
					throw ((InvalidPropValue)o).Error;
				}

				return o;
			}

			set {
				SetPropertyValue(prop, value,false); // Do not ignore locks!!!
			}
		}

		protected internal Object this[String propertyName] {
			get {
				ConfigurationProperty prop = Properties[propertyName];
				if (prop == null) {
					prop = Properties[DefaultCollectionPropertyName];
					if (prop.ProvidedName != propertyName) {
						return null;
					}
				}
				return this[prop];
			}
			set {
				Debug.Assert(Properties.Contains(propertyName), "Properties.Contains(propertyName)");
				SetPropertyValue(Properties[propertyName], value, false);// Do not ignore locks!!!
			}
		}

                /* Pulled in from reference source. */
                private static bool PropertiesFromType(Type type, out ConfigurationPropertyCollection result) {
                    ConfigurationPropertyCollection properties = (ConfigurationPropertyCollection)s_propertyBags[type];
                    result = null;
                    bool firstTimeInit = false;
                    if (properties == null) {
                        lock (s_propertyBags.SyncRoot) {
                            properties = (ConfigurationPropertyCollection)s_propertyBags[type];
                            if (properties == null) {
                                properties = CreatePropertyBagFromType(type);
                                s_propertyBags[type] = properties;
                                firstTimeInit = true;
                            }
                        }
                    }
                    result = properties;
                    return firstTimeInit;
                }

                private static ConfigurationPropertyCollection CreatePropertyBagFromType(Type type) {
                    Debug.Assert(type != null, "type != null");

                    // For ConfigurationElement derived classes - get the per-type validator
                    if (typeof(ConfigurationElement).IsAssignableFrom(type)) {
                        ConfigurationValidatorAttribute attribValidator = Attribute.GetCustomAttribute(type, typeof(ConfigurationValidatorAttribute)) as ConfigurationValidatorAttribute;

                        if (attribValidator != null) {
                            attribValidator.SetDeclaringType(type);
                            ConfigurationValidatorBase validator = attribValidator.ValidatorInstance;

                            if (validator != null) {
                                CachePerTypeValidator(type, validator);
                            }
                        }
                    }

                    ConfigurationPropertyCollection properties = new ConfigurationPropertyCollection();

                    foreach (PropertyInfo propertyInformation in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                        ConfigurationProperty newProp = CreateConfigurationPropertyFromAttributes(propertyInformation);

                        if (newProp != null) {
                            properties.Add(newProp);
                        }
                    }

                    return properties;
                }

                private static ConfigurationProperty CreateConfigurationPropertyFromAttributes(PropertyInfo propertyInformation) {
                    ConfigurationProperty result = null;

                    ConfigurationPropertyAttribute attribProperty =
                        Attribute.GetCustomAttribute(propertyInformation,
                                                        typeof(ConfigurationPropertyAttribute)) as ConfigurationPropertyAttribute;

                    // If there is no ConfigurationProperty attrib - this is not considered a property
                    if (attribProperty != null) {
                        result = new ConfigurationProperty(propertyInformation);
                    }

                    // Handle some special cases of property types
                    if (result != null && typeof(ConfigurationElement).IsAssignableFrom(result.Type)) {
                        ConfigurationPropertyCollection unused = null;

                        PropertiesFromType(result.Type, out unused);
                    }

                    return result;
                }

                private static void CachePerTypeValidator( Type type, ConfigurationValidatorBase validator ) {
                    Debug.Assert((type != null) && ( validator != null));
                    Debug.Assert(typeof(ConfigurationElement).IsAssignableFrom(type));

                    // Use the same lock as the property bag lock since in the current implementation
                    // the only way to get to this method is through the code path that locks the property bag cache first ( see PropertiesFromType() )

                    // NOTE[ Thread Safety ]: Non-guarded access to static variable - since this code is called only from CreatePropertyBagFromType
                    // which in turn is done onle once per type and is guarded by the s_propertyBag.SyncRoot then this call is thread safe as well
                    if (s_perTypeValidators == null ) {
                            s_perTypeValidators = new Dictionary<Type,ConfigurationValidatorBase>();
                    }

                    // A type validator should be cached only once. If it isn't then attribute parsing is done more then once which should be avoided
                    Debug.Assert( !s_perTypeValidators.ContainsKey(type));

                    // Make sure the supplied validator supports validating this object
                    if (!validator.CanValidate(type)) {
                        throw new ConfigurationErrorsException("Validator does not support element type");
                    }

                    s_perTypeValidators.Add(type, validator);
                }

                private static void ApplyValidatorsRecursive(ConfigurationElement root) {
                    Debug.Assert(root != null);

                    // Apply the validator on 'root'
                    ApplyValidator(root);

                    // Apply validators on child elements ( note - we will do this only on already created child elements
                    // The non created ones will get their validators in the ctor
                    foreach (ConfigurationElement elem in root._values.ConfigurationElements) {
                        ApplyValidatorsRecursive(elem);
                    }
                }

                private static void ApplyValidator(ConfigurationElement elem) {
                    Debug.Assert(elem != null);

                    if ((s_perTypeValidators != null) && (s_perTypeValidators.ContainsKey(elem.GetType()))) {
                        elem._elementProperty = new ConfigurationElementProperty(s_perTypeValidators[ elem.GetType() ]);
                    }
                }

               protected internal virtual ConfigurationPropertyCollection Properties {
			get {
				ConfigurationPropertyCollection result = null;

				if (PropertiesFromType(this.GetType(), out result))
					ApplyValidatorsRecursive(this);
				return result;
			}
		}

		/* Begin referencesource. */
		internal ConfigurationValues Values {
				get {
					return _values;
				}
		}

		internal PropertySourceInfo PropertyInfoInternal(string propertyName) {
				return (PropertySourceInfo)_values.GetSourceInfo(propertyName);
		}

		internal string PropertyFileName(string propertyName) {
				PropertySourceInfo p = (PropertySourceInfo)PropertyInfoInternal(propertyName);
				if (p == null)
					p = (PropertySourceInfo)PropertyInfoInternal(String.Empty); // Get the filename of the parent if prop is not there
				if (p == null)
					return String.Empty;
				return p.FileName;
		}

		internal int PropertyLineNumber(string propertyName) {
				PropertySourceInfo p = (PropertySourceInfo)PropertyInfoInternal(propertyName);
				if (p == null)
					p = (PropertySourceInfo)PropertyInfoInternal(String.Empty);
				if (p == null)
					return 0;
				return p.LineNumber;
		}
		/* End referencesource. */

		public override bool Equals (object compareTo)
		{
			ConfigurationElement other = compareTo as ConfigurationElement;
			if (other == null) return false;
			if (GetType() != other.GetType()) return false;
			
			foreach (ConfigurationProperty prop in Properties) {
				if (!object.Equals (this [prop], other [prop]))
					return false;
			}
			return true;
		}

		public override int GetHashCode ()
		{
			int code = 0;
			object o;
			
			foreach (ConfigurationProperty prop in Properties) {
				o = this [prop];
				if (o == null)
					continue;
				
				code += o.GetHashCode ();
			}
			
			return code;
		}

		internal virtual bool HasLocalModifications ()
		{
			foreach (PropertyInformation pi in ElementInformation.Properties)
				if (pi.ValueOrigin == PropertyValueOrigin.SetHere && pi.IsModified)
					return true;
			
			return false;
		}
		
		protected internal virtual void DeserializeElement (XmlReader reader, bool serializeCollectionKey)
		{
			Hashtable readProps = new Hashtable ();
			
			reader.MoveToContent ();

			ConfigXmlTextReader _reader = reader as ConfigXmlTextReader;
			if (_reader != null) {
				PropertySourceInfo rootInfo = new PropertySourceInfo(_reader);
				_elementTagName = _reader.Name;
				_values.SetValue(_reader.Name, null, ConfigurationValueFlags.Modified, rootInfo);
				_values.SetValue(DefaultCollectionPropertyName, defaultCollection, ConfigurationValueFlags.Modified, rootInfo);
				elementPresent = true;
			}

			while (reader.MoveToNextAttribute ())
			{
				PropertyInformation prop = ElementInformation.Properties [reader.LocalName];
				if (prop == null || (serializeCollectionKey && !prop.IsKey)) {
					/* handle the built in ConfigurationElement attributes here */
					if (reader.LocalName == "lockAllAttributesExcept") {
						LockAllAttributesExcept.SetFromList (reader.Value);
					}
					else if (reader.LocalName == "lockAllElementsExcept") {
						LockAllElementsExcept.SetFromList (reader.Value);
					}
					else if (reader.LocalName == "lockAttributes") {
						LockAttributes.SetFromList (reader.Value);
					}
					else if (reader.LocalName == "lockElements") {
						LockElements.SetFromList (reader.Value);
					}
					else if (reader.LocalName == "lockItem") {
						LockItem = (reader.Value.ToLowerInvariant () == "true");
					}
					else if (reader.LocalName == "xmlns") {
						/* ignore */
					} else if (this is ConfigurationSection && reader.LocalName == "configSource") {
						/* ignore */
					} else if (!OnDeserializeUnrecognizedAttribute (reader.LocalName, reader.Value))
						throw new ConfigurationErrorsException ("Unrecognized attribute '" + reader.LocalName + "'.", reader);

					continue;
				}
				
				if (readProps.ContainsKey (prop))
					throw new ConfigurationErrorsException ("The attribute '" + prop.Name + "' may only appear once in this element.", reader);

				_values.SetValue(prop.Name,
						    DeserializePropertyValue(Properties[reader.Name], reader),
						    ConfigurationValueFlags.Modified,
						    new PropertySourceInfo(reader));
				readProps [prop] = prop.Name;
			}
			
			reader.MoveToElement ();
			if (reader.IsEmptyElement) {
				reader.Skip ();
			} else {
				int depth = reader.Depth;

				reader.ReadStartElement ();
				reader.MoveToContent ();

				do {
					if (reader.NodeType != XmlNodeType.Element) {
						reader.Skip ();
						continue;
					}
					
					PropertyInformation prop = ElementInformation.Properties [reader.LocalName];
					if (prop == null || (serializeCollectionKey && !prop.IsKey)) {
						if (!OnDeserializeUnrecognizedElement (reader.LocalName, reader)) {
							if (prop == null) {
								ConfigurationElementCollection c = GetDefaultCollection ();
								if (c != null && c.OnDeserializeUnrecognizedElement (reader.LocalName, reader))
									continue;
							}
							throw new ConfigurationErrorsException ("Unrecognized element '" + reader.LocalName + "'.", reader);
						}
						continue;
					}
					
					if (!prop.IsElement)
						throw new ConfigurationErrorsException ("Property '" + prop.Name + "' is not a ConfigurationElement.");
					
					if (readProps.Contains (prop))
						throw new ConfigurationErrorsException ("The element <" + prop.Name + "> may only appear once in this section.", reader);
					
					ConfigurationElement val = (ConfigurationElement) prop.Value;
					val.DeserializeElement (reader, serializeCollectionKey);
					readProps [prop] = prop.Name;

					if(depth == reader.Depth)
						reader.Read();

				} while (depth < reader.Depth);				
			}
			
			modified = false;
				
			foreach (PropertyInformation prop in ElementInformation.Properties)
				if (!String.IsNullOrEmpty(prop.Name) && prop.IsRequired && !readProps.ContainsKey (prop)) {
					PropertyInformation p = ElementInformation.Properties [prop.Name];
					if (p == null) {
						object val = OnRequiredPropertyNotFound (prop.Name);

						if (!object.Equals (val, prop.DefaultValue))
							_values.SetValue(prop.Name, val, ConfigurationValueFlags.Default, null);

					}
				}

			PostDeserialize ();
		}

		/* From referencesource. */
		private object DeserializePropertyValue(ConfigurationProperty prop, XmlReader reader) {
			Debug.Assert(prop != null, "prop != null");
			Debug.Assert(reader != null, "reader != null");

			// By default we try to load (i.e. parse/validate ) all properties
			// If a property value is invalid ( cannot be parsed or is not valid ) we will keep the value
			// as string ( from the xml ) and will write it out unchanged if needed
			// If the property value is needed by users the actuall exception will be thrown

			string xmlValue = reader.Value;
			object propertyValue = null;

			try {
				propertyValue = prop.ConvertFromString(xmlValue);

				// Validate the loaded and converted value
				prop.Validate(propertyValue);
			}
			catch (ConfigurationException ce) {
				// If the error is incomplete - complete it :)
				if (string.IsNullOrEmpty(ce.Filename)) {
					ce = new ConfigurationErrorsException(ce.Message, reader);
				}

				// Cannot parse/validate the value. Keep it as string
				propertyValue = new InvalidPropValue(xmlValue, ce);
			}
			catch {
				// If this is an exception related to the parsing/validating the
				// value ConfigurationErrorsException should be thrown instead.
				// If not - the exception is ok to surface out of here
				Debug.Fail("Unknown exception type thrown");
			}

			return propertyValue;
		}

		protected virtual bool OnDeserializeUnrecognizedAttribute (string name, string value)
		{
			return false;
		}

		protected virtual bool OnDeserializeUnrecognizedElement (string elementName, XmlReader reader)
		{
			return false;
		}
		
		protected virtual object OnRequiredPropertyNotFound (string name)
		{
			throw new ConfigurationErrorsException ("Required attribute '" + name + "' not found.");
		}
		
		protected virtual void PreSerialize (XmlWriter writer)
		{
		}

		protected virtual void PostDeserialize ()
		{
		}

		protected internal virtual void InitializeDefault ()
		{
		}

		/* Begin referencesource. */
		protected internal virtual bool IsModified() {
			if (modified) {
				return true;
			}

			/* FIXME:LOCKING: We haven't pulled over the locking code from referencesource.
			if (_lockedAttributesList != null && _lockedAttributesList.IsModified) {
				return true;
			}

			if (_lockedAllExceptAttributesList != null && _lockedAllExceptAttributesList.IsModified) {
				return true;
			}

			if (_lockedElementsList != null && _lockedElementsList.IsModified) {
				return true;
			}

			if (_lockedAllExceptElementsList != null && _lockedAllExceptElementsList.IsModified) {
				return true;
			}

			if ((_fItemLocked & ConfigurationValueFlags.Modified) != 0) {
				return true;
			}
			*/

			foreach (ConfigurationElement elem in _values.ConfigurationElements) {
				if (elem.IsModified()) {
					return true;
				}
			}
			return false;
		}

		protected internal virtual void ResetModified() {
			modified = false;
			/* FIXME:LOCKING: We haven't pulled over the locking code from referencesource.
			if (_lockedAttributesList != null) {
				_lockedAttributesList.ResetModified();
			}

			if (_lockedAllExceptAttributesList != null) {
				_lockedAllExceptAttributesList.ResetModified();
			}

			if (_lockedElementsList != null) {
				_lockedElementsList.ResetModified();
			}

			if (_lockedAllExceptElementsList != null) {
				_lockedAllExceptElementsList.ResetModified();
			}
			*/
			foreach (ConfigurationElement elem in _values.ConfigurationElements) {
				elem.ResetModified();
			}
		}
		/* End referencesource. */

		protected internal virtual void SetReadOnly ()
		{
			readOnly = true;
		}
		
		public virtual bool IsReadOnly ()
		{
			return readOnly;
		}

		/* From referencesource. */
		protected internal virtual void Reset(ConfigurationElement parentElement) {
			Values.Clear();
//			ResetLockLists(parentElement); FIXME:LOCKING.
			ConfigurationPropertyCollection props = Properties; // Force the bag to be up to date
			elementPresent = false;
			if (parentElement == null) {
				InitializeDefault();
			}
			else {
				bool hasAnyChildElements = false;

				ConfigurationPropertyCollection collectionKeys = null;

				for (int index = 0; index < parentElement.Values.Count; index++) {
					string key = parentElement.Values.GetKey(index);
					ConfigurationValue ConfigValue = parentElement.Values.GetConfigValue(index);
					object value = (ConfigValue != null) ? ConfigValue.Value : null;
					PropertySourceInfo sourceInfo = (ConfigValue != null) ? ConfigValue.SourceInfo : null;

					ConfigurationProperty prop = (ConfigurationProperty)parentElement.Properties[key];
					if (prop == null || ((collectionKeys != null) && !collectionKeys.Contains(prop.Name))) {
						continue;
					}

					if (prop.IsConfigurationElementType) {
						hasAnyChildElements = true;
					}
					else {
						ConfigurationValueFlags flags = ConfigurationValueFlags.Inherited | ConfigurationValueFlags.Default;
						/* FIXME:LOCKING: No locking in our ConfigurationElement.
						(((_lockedAttributesList != null) &&
							  (_lockedAttributesList.Contains(key) ||
							   _lockedAttributesList.Contains(LockAll)) ||
							  (_lockedAllExceptAttributesList != null) &&
							  !_lockedAllExceptAttributesList.Contains(key)) ?
							  ConfigurationValueFlags.Locked : ConfigurationValueFlags.Default);
						*/
						if (value != s_nullPropertyValue) {
							// _values[key] = value;
							_values.SetValue(key, value, flags, sourceInfo);
						}
						if (!props.Contains(key)) // this is for optional provider models keys
						{
							props.Add(prop);
							_values.SetValue(key, value, flags, sourceInfo);
						}
					}
				}

				if (hasAnyChildElements) {
					for (int index = 0; index < parentElement.Values.Count; index++) {
						string key = parentElement.Values.GetKey(index);
						object value = parentElement.Values[index];

						ConfigurationProperty prop = (ConfigurationProperty)parentElement.Properties[key];
						if ((prop != null) && prop.IsConfigurationElementType) {
							//((ConfigurationElement)value).SerializeToXmlElement(writer, prop.Name);
							ConfigurationElement childElement = (ConfigurationElement)this[prop];
							childElement.Reset((ConfigurationElement)value);
						}
					}
				}
			}
		}

		protected internal virtual bool SerializeElement (XmlWriter writer, bool serializeCollectionKey)
		{
                        bool wroteData = _bDataToWrite;
			PreSerialize (writer);
			
			if (serializeCollectionKey) {
				ConfigurationPropertyCollection props = GetKeyProperties ();
				foreach (ConfigurationProperty prop in props) {
					if (writer != null) writer.WriteAttributeString (prop.Name, prop.ConvertToString (this[prop.Name]));
				}
				return (props.Count > 0) || wroteData;
			}
			
			foreach (PropertyInformation prop in ElementInformation.Properties)
			{
				if (prop.IsElement)
					continue;

				if (saveContext == null)
					throw new InvalidOperationException ();
				if (!saveContext.HasValue (prop))
					continue;

				if (writer != null) writer.WriteAttributeString (prop.Name, prop.GetStringValue ());
				wroteData = true;
			}
			
			foreach (PropertyInformation prop in ElementInformation.Properties)
			{
				if (!prop.IsElement)
					continue;
				
				ConfigurationElement val = (ConfigurationElement) prop.Value;
				if (val != null)
					wroteData = val.SerializeToXmlElement (writer, prop.Name) || wroteData;
			}
			return wroteData;
		}

		protected internal virtual bool SerializeToXmlElement (
				XmlWriter writer, string elementName)
		{
			if (saveContext == null)
				throw new InvalidOperationException ();
			if (!saveContext.HasValues ())
				return false;

			bool res = SerializeElement(null, false);
			if (res == true) {
				if (writer != null && elementName != null && elementName != "")
					writer.WriteStartElement (elementName);
				res = SerializeElement (writer, false);
				if (writer != null && elementName != null && elementName != "")
					writer.WriteEndElement ();
			}
			return res;
		}

		/* From referencesource. */
		protected internal virtual void Unmerge(ConfigurationElement sourceElement,
							ConfigurationElement parentElement,
							ConfigurationSaveMode saveMode) {
			if (sourceElement != null) {
				bool hasAnyChildElements = false;

				/* FIXME:LOCKING.
				_lockedAllExceptAttributesList = sourceElement._lockedAllExceptAttributesList;
				_lockedAllExceptElementsList = sourceElement._lockedAllExceptElementsList;
				_fItemLocked = sourceElement._fItemLocked;
				_lockedAttributesList = sourceElement._lockedAttributesList;
				_lockedElementsList = sourceElement._lockedElementsList;
				*/

				AssociateContext(sourceElement.Configuration);

				/* FIXME:LOCKING.
				if (parentElement != null) {
					if (parentElement._lockedAttributesList != null)
						_lockedAttributesList = UnMergeLockList(sourceElement._lockedAttributesList,
							parentElement._lockedAttributesList, saveMode);
					if (parentElement._lockedElementsList != null)
						_lockedElementsList = UnMergeLockList(sourceElement._lockedElementsList,
							parentElement._lockedElementsList, saveMode);
					if (parentElement._lockedAllExceptAttributesList != null)
						_lockedAllExceptAttributesList = UnMergeLockList(sourceElement._lockedAllExceptAttributesList,
							parentElement._lockedAllExceptAttributesList, saveMode);
					if (parentElement._lockedAllExceptElementsList != null)
						_lockedAllExceptElementsList = UnMergeLockList(sourceElement._lockedAllExceptElementsList,
							parentElement._lockedAllExceptElementsList, saveMode);
				}
				*/

				ConfigurationPropertyCollection props = Properties;
				ConfigurationPropertyCollection collectionKeys = null;

				// check for props not in bag from source
				for (int index = 0; index < sourceElement.Values.Count; index++) {
					string key = sourceElement.Values.GetKey(index);
					object value = sourceElement.Values[index];
					ConfigurationProperty prop = (ConfigurationProperty)sourceElement.Properties[key];
					if (prop == null || (collectionKeys != null && !collectionKeys.Contains(prop.Name)))
						continue;
					if (prop.IsConfigurationElementType) {
						hasAnyChildElements = true;
					}
					else {
						if (value != s_nullPropertyValue) {
							if (!props.Contains(key)) // this is for optional provider models keys
							{
								// _values[key] = value;
								ConfigurationValueFlags valueFlags = sourceElement.Values.RetrieveFlags(key);
								_values.SetValue(key, value, valueFlags, null);

								props.Add(prop);
							}
						}
					}
				}

				foreach (ConfigurationProperty prop in Properties) {
					if (prop == null || (collectionKeys != null && !collectionKeys.Contains(prop.Name))) {
						continue;
					}
					if (prop.IsConfigurationElementType) {
						hasAnyChildElements = true;
					}
					else {
						object value = sourceElement.Values[prop.Name];

						// if the property is required or we are writing a full config make sure we have defaults
						if ((prop.IsRequired == true || saveMode == ConfigurationSaveMode.Full) && (value == null || value == s_nullPropertyValue)) {
							// If the default value is null, this means there wasnt a reasonable default for the value
							// and there is nothing more we can do. Otherwise reset the value to the default

							// Note: 'null' should be used as default for non-empty strings instead
							// of the current practice to use String.Epmty

							if (prop.DefaultValue != null) {
								value = prop.DefaultValue; // need to make sure required properties are persisted
							}
						}

						if (value != null && value != s_nullPropertyValue) {
							object value2 = null;
							if (parentElement != null) // Is there a parent
								value2 = parentElement.Values[prop.Name]; // if so get it's value

							if (value2 == null) // no parent use default
								value2 = prop.DefaultValue;
							// If changed and not same as parent write or required

							switch (saveMode) {
								case ConfigurationSaveMode.Minimal: {
										if (!Object.Equals(value, value2) || prop.IsRequired == true)
											_values[prop.Name] = value;
									}
									break;
								// (value != null && value != s_nullPropertyValue) ||
								case ConfigurationSaveMode.Modified: {
										bool modified = sourceElement.Values.IsModified(prop.Name);
										bool inherited = sourceElement.Values.IsInherited(prop.Name);

										// update the value if the property is required, modified or it was not inherited
										// Also update properties that ARE inherited when we are resetting the object
										// as long as the property is not the same as the default value for the property
										if ((prop.IsRequired || modified || !inherited) ||
											(parentElement == null && inherited && !Object.Equals(value, value2))) {
											_values[prop.Name] = value;
										}
									}
									break;
								case ConfigurationSaveMode.Full: {
										if (value != null && value != s_nullPropertyValue)
											_values[prop.Name] = value;
										else
											_values[prop.Name] = value2;

									}
									break;
							}
						}
					}
				}

				if (hasAnyChildElements) {
					foreach (ConfigurationProperty prop in Properties) {
						if (prop.IsConfigurationElementType) {
							ConfigurationElement pElem = (ConfigurationElement)((parentElement != null) ? parentElement[prop] : null);
							ConfigurationElement childElement = (ConfigurationElement)this[prop];
							if ((ConfigurationElement)sourceElement[prop] != null)
								childElement.Unmerge((ConfigurationElement)sourceElement[prop],
									pElem, saveMode);
						}

					}
				}
			}
		}

		internal bool HasValue (string propName)
		{
			PropertyInformation info = ElementInformation.Properties [propName];
			return info != null && info.ValueOrigin != PropertyValueOrigin.Default;
		}
		
		internal bool IsReadFromConfig (string propName)
		{
			PropertyInformation info = ElementInformation.Properties [propName];
			return info != null && info.ValueOrigin == PropertyValueOrigin.SetHere;
		}

		internal bool IsElementPresent
		{
			get {	return elementPresent;	}
		}

                internal bool ElementPresent {
                    get {
                        return elementPresent;
                    }
                    set {
                        elementPresent = value;
                    }
                }

                internal string ElementTagName {
                    get {
                        return _elementTagName;
                    }
                }

		void ValidateValue (ConfigurationProperty p, string value)
		{
			ConfigurationValidatorBase validator;
			if (p == null || (validator = p.Validator) == null)
				return;
			
			if (!validator.CanValidate (p.Type))
				throw new ConfigurationErrorsException (
					String.Format ("Validator does not support type {0}", p.Type));
			validator.Validate (p.ConvertFromString (value));
		}

		// AssociateContext
		//
		// Associate a context with this element
		//
		internal virtual void AssociateContext(Configuration config) {
		    Configuration = config;
		    Values.AssociateContext(config);
		}

		/*
		 * FIXME: LAMESPEC
		 * 
		 * SerializeElement() and SerializeToXmlElement() need to emit different output
		 * based on the ConfigurationSaveMode that's being used.  Unfortunately, neither
		 * of these methods take it as an argument and there seems to be no documented way
		 * how to get it.
		 * 
		 * The parent element is needed because the element could be set to a different
		 * than the default value in a parent configuration file, then set locally to that
		 * same value.  This makes the element appear locally modified (so it's included
		 * with ConfigurationSaveMode.Modified), but it should not be emitted with
		 * ConfigurationSaveMode.Minimal.
		 * 
		 * In theory, we could save it into some private field in Unmerge(), but the
		 * problem is that Unmerge() is kinda expensive and we also need a way of
		 * determining whether or not the configuration has changed in Configuration.Save(),
		 * prior to opening the output file for writing.
		 * 
		 * There are two places from where HasValues() is called:
		 * a) From Configuration.Save() / SaveAs() to check whether the configuration needs
		 *    to be saved.  This check is done prior to opening the file for writing.
		 * b) From SerializeToXmlElement() to check whether to emit the element, using the
		 *    parent and mode values from the cached 'SaveContext'.
		 * 
		 */

		/*
		 * Check whether property 'prop' should be included in the serialized XML
		 * based on the current ConfigurationSaveMode.
		 */
		internal bool HasValue (ConfigurationElement parent, PropertyInformation prop,
		                        ConfigurationSaveMode mode)
		{
			if (prop.ValueOrigin == PropertyValueOrigin.Default)
				return false;
			
			if (mode == ConfigurationSaveMode.Modified &&
			    prop.ValueOrigin == PropertyValueOrigin.SetHere && prop.IsModified) {
				// Value has been modified locally, so we always emit it
				// with ConfigurationSaveMode.Modified.
				return true;
			}

			/*
			 * Ok, now we have to check whether we're different from the inherited
			 * value - which could either be a value that's set in a parent
			 * configuration file or the default value.
			 */
			
			var hasParentValue = parent != null && parent.HasValue (prop.Name);
			var parentOrDefault = hasParentValue ? parent [prop.Name] : prop.DefaultValue;

			if (!prop.IsElement)
				return !object.Equals (prop.Value, parentOrDefault);

			/*
			 * Ok, it's an element that has been set in a parent configuration file.			 * 
			 * Recursively call HasValues() to check whether it's been locally modified.
			 */
			var element = (ConfigurationElement) prop.Value;
			var parentElement = (ConfigurationElement) parentOrDefault;
			
			return element.HasValues (parentElement, mode);
		}

		/*
		 * Check whether this element should be included in the serialized XML
		 * based on the current ConfigurationSaveMode.
		 * 
		 * The 'parent' value is needed to determine whether the element currently
		 * has a different value from what's been set in the parent configuration
		 * hierarchy.
		 */
		internal virtual bool HasValues (ConfigurationElement parent, ConfigurationSaveMode mode)
		{
			if (mode == ConfigurationSaveMode.Full)
				return true;
			if (IsModified()/*modified*/ && (mode == ConfigurationSaveMode.Modified))
				return true;
			
			foreach (PropertyInformation prop in ElementInformation.Properties) {
				if (HasValue (parent, prop, mode))
					return true;
			}
			
			return false;
		}

		/*
		 * Cache the current 'parent' and 'mode' values for later use in SerializeToXmlElement()
		 * and SerializeElement().
		 * 
		 * Make sure to call base when overriding this in a derived class.
		 */
		internal virtual void PrepareSave (ConfigurationElement parent, ConfigurationSaveMode mode)
		{
			saveContext = new SaveContext (this, parent, mode);

			foreach (PropertyInformation prop in ElementInformation.Properties)
			{
				if (!prop.IsElement)
					continue;

				var elem = (ConfigurationElement)prop.Value;
				if (parent == null || !parent.HasValue (prop.Name))
					elem.PrepareSave (null, mode);
				else {
					var parentValue = (ConfigurationElement)parent [prop.Name];
					elem.PrepareSave (parentValue, mode);
				}
			}
		}

		SaveContext saveContext;

		class SaveContext {
			public readonly ConfigurationElement Element;
			public readonly ConfigurationElement Parent;
			public readonly ConfigurationSaveMode Mode;

			public SaveContext (ConfigurationElement element, ConfigurationElement parent,
			                    ConfigurationSaveMode mode)
			{
				this.Element = element;
				this.Parent = parent;
				this.Mode = mode;
			}

			public bool HasValues ()
			{
				if (Mode == ConfigurationSaveMode.Full)
					return true;
				return Element.HasValues (Parent, Mode);
			}

			public bool HasValue (PropertyInformation prop)
			{
				if (Mode == ConfigurationSaveMode.Full)
					return true;
				return Element.HasValue (Parent, prop, Mode);
			}
		}
	}
}

